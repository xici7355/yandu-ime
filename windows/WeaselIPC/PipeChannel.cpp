#include "stdafx.h"

#include <PipeChannel.h>

using namespace weasel;
using namespace std;
using namespace boost;

#define _ThrowLastError throw ::GetLastError()
#define _ThrowCode(__c) throw static_cast<DWORD>(__c)
#define _ThrowIfNot(__c)                 \
  {                                      \
    DWORD err;                           \
    if ((err = ::GetLastError()) != __c) \
      throw err;                         \
  }

// Only client handles use overlapped I/O. An unresponsive service must not
// leave the host application's keyboard thread inside an unbounded ReadFile
// or FlushFileBuffers call. Cancel the local pipe operation before unwinding.
static DWORD ClientTransfer(HANDLE pipe, void* buffer, DWORD size, bool write,
                            bool& more) {
  OVERLAPPED operation{};
  operation.hEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
  if (!operation.hEvent) _ThrowLastError;
  DWORD transferred = 0;
  BOOL completed = write ? WriteFile(pipe, buffer, size, &transferred, &operation)
                         : ReadFile(pipe, buffer, size, &transferred, &operation);
  DWORD error = completed ? ERROR_SUCCESS : GetLastError();
  if (!completed && error == ERROR_IO_PENDING) {
    const DWORD waited = WaitForSingleObject(operation.hEvent, 1500);
    if (waited != WAIT_OBJECT_0) {
      CancelIoEx(pipe, &operation);
      // Named-pipe cancellation completes before the stack buffer goes away.
      GetOverlappedResult(pipe, &operation, &transferred, TRUE);
      CloseHandle(operation.hEvent);
      _ThrowCode(ERROR_TIMEOUT);
    }
    completed = GetOverlappedResult(pipe, &operation, &transferred, FALSE);
    error = completed ? ERROR_SUCCESS : GetLastError();
  }
  CloseHandle(operation.hEvent);
  more = error == ERROR_MORE_DATA;
  if (error != ERROR_SUCCESS && !more) _ThrowCode(error);
  return transferred;
}

PipeChannelBase::PipeChannelBase(std::wstring&& pn_cmd,
                                 size_t bs = 4 * 1024,
                                 SECURITY_ATTRIBUTES* s = NULL)
    : pname(pn_cmd), buff_size(bs), sa(s) {};

PipeChannelBase::~PipeChannelBase() {
  // Thread-specific pointers are cleaned up automatically
}

bool PipeChannelBase::_Ensure() {
  try {
    HANDLE* phandle = _GetPipeHandle();
    if (_Invalid(*phandle)) {
      *phandle = _Connect(pname.c_str());
      return !_Invalid(*phandle);
    }
  } catch (...) {
    return false;
  }

  return true;
}

HANDLE PipeChannelBase::_Connect(const wchar_t* name) {
  HANDLE pipe = INVALID_HANDLE_VALUE;
  const auto until = GetTickCount64() + 500;
  while (_Invalid(pipe = _TryConnect())) {
    if (GetTickCount64() >= until) _ThrowCode(ERROR_TIMEOUT);
    ::WaitNamedPipe(name, 100);
  }
  DWORD mode = PIPE_READMODE_MESSAGE;
  if (!SetNamedPipeHandleState(pipe, &mode, NULL, NULL)) {
    _ThrowLastError;
  }
  return pipe;
}

void PipeChannelBase::_Reconnect() {
  HANDLE* phandle = _GetPipeHandle();
  _FinalizePipe(*phandle);
  _Ensure();
}

HANDLE PipeChannelBase::_TryConnect() {
  auto pipe = ::CreateFile(pname.c_str(), GENERIC_READ | GENERIC_WRITE, 0, NULL,
                           OPEN_EXISTING, FILE_FLAG_OVERLAPPED, NULL);
  if (!_Invalid(pipe)) {
    _GetContext()->client_io = true;
    // connected to the pipe
    return pipe;
  }
  // being busy is not really an error since we just need to wait.
  _ThrowIfNot(ERROR_PIPE_BUSY);
  // All pipe instances are busy
  return INVALID_HANDLE_VALUE;
}

size_t PipeChannelBase::_WritePipe(HANDLE pipe, size_t s, char* b) {
  if (_GetContext()->client_io) {
    bool more = false;
    const auto written = ClientTransfer(pipe, b, static_cast<DWORD>(s), true, more);
    if (written != s) _ThrowCode(ERROR_WRITE_FAULT);
    return written;
  }
  DWORD lwritten;
  if (!::WriteFile(pipe, b, s, &lwritten, NULL) || lwritten <= 0) {
    _ThrowLastError;
  }
  ::FlushFileBuffers(pipe);
  return lwritten;
}

void PipeChannelBase::_FinalizePipe(HANDLE& p) {
  if (!_Invalid(p)) {
    DisconnectNamedPipe(p);
    CloseHandle(p);
  }
  p = INVALID_HANDLE_VALUE;
}

void PipeChannelBase::_Receive(HANDLE pipe, LPVOID msg, size_t rec_len) {
  if (_GetContext()->client_io) {
    bool more = false;
    const auto count = ClientTransfer(pipe, msg, static_cast<DWORD>(rec_len), false, more);
    if (count != rec_len) _ThrowCode(ERROR_READ_FAULT);
    if (more) {
      auto ctx = _GetContext();
      memset(ctx->buffer.get(), 0, buff_size);
      ClientTransfer(pipe, ctx->buffer.get(), static_cast<DWORD>(buff_size), false, more);
      if (more) _ThrowCode(ERROR_BUFFER_OVERFLOW);
    }
    _GetContext()->has_body = false;
    return;
  }
  DWORD lread;
  BOOL success = ::ReadFile(pipe, msg, rec_len, &lread, NULL);
  if (!success) {
    _ThrowIfNot(ERROR_MORE_DATA);

    auto ctx = _GetContext();
    memset(ctx->buffer.get(), 0, buff_size);
    success = ::ReadFile(pipe, ctx->buffer.get(), buff_size, &lread, NULL);
    if (!success) {
      _ThrowLastError;
    }
  }
  _GetContext()->has_body = false;
}

HANDLE PipeChannelBase::_ConnectServerPipe(std::wstring& pn) {
  HANDLE pipe =
      CreateNamedPipe(pn.c_str(), PIPE_ACCESS_DUPLEX,
                      PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                      PIPE_UNLIMITED_INSTANCES, buff_size, buff_size, 0, sa);
  if (pipe == INVALID_HANDLE_VALUE) _ThrowLastError;
  if (!::ConnectNamedPipe(pipe, NULL)) {
    const DWORD error = GetLastError();
    // A client can connect between CreateNamedPipe and ConnectNamedPipe.
    // ERROR_PIPE_CONNECTED is a successful connection, not an abandoned pipe.
    if (error != ERROR_PIPE_CONNECTED) {CloseHandle(pipe); _ThrowCode(error);}
  }
  return pipe;
}
