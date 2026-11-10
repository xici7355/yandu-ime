#include <windows.h>
#include <thread>
#include <iostream>
#include <string>
#include <cstring>
#include <functional>
#include "PipeChannel.h"
static bool run(bool stalled){
 std::wstring name=L"\\\\.\\pipe\\YanduPipeTest-"+std::to_wstring(GetCurrentProcessId())+(stalled?L"-stall":L"-normal");
 HANDLE ready=CreateEventW(nullptr,TRUE,FALSE,nullptr),stop=CreateEventW(nullptr,TRUE,FALSE,nullptr);
 std::thread server([&]{HANDLE pipe=CreateNamedPipeW(name.c_str(),PIPE_ACCESS_DUPLEX,PIPE_TYPE_MESSAGE|PIPE_READMODE_MESSAGE|PIPE_WAIT,1,65536,65536,0,nullptr);SetEvent(ready);if(pipe!=INVALID_HANDLE_VALUE&&(ConnectNamedPipe(pipe,nullptr)||GetLastError()==ERROR_PIPE_CONNECTED)){
  DWORD request=0,count=0;ReadFile(pipe,&request,sizeof(request),&count,nullptr);if(!stalled){struct Reply{DWORD header;wchar_t body[16];} reply{};reply.header=123;wcscpy_s(reply.body,L"fixture");WriteFile(pipe,&reply,sizeof(reply),&count,nullptr);FlushFileBuffers(pipe);}WaitForSingleObject(stop,3500);DisconnectNamedPipe(pipe);
 }if(pipe!=INVALID_HANDLE_VALUE)CloseHandle(pipe);});
 WaitForSingleObject(ready,1000);weasel::PipeChannel<DWORD> client{std::wstring(name)};DWORD value=1;auto began=GetTickCount64();bool ok=false;
 try{auto result=client.Transact(value);std::function<bool(LPWSTR,UINT)> check=[](LPWSTR text,UINT){return std::wstring(text)==L"fixture";};ok=!stalled&&result==123&&client.HandleResponseData(check);}
 catch(DWORD error){auto elapsed=GetTickCount64()-began;std::cout<<"error="<<error<<" elapsedMs="<<elapsed<<"\n";ok=stalled&&error==ERROR_TIMEOUT&&elapsed>=1000&&elapsed<3000;}
 client.Disconnect();SetEvent(stop);server.join();CloseHandle(ready);CloseHandle(stop);return ok;
}
int main(){if(!run(false)){std::cerr<<"FAIL normal message/header/body I/O\n";return 1;}std::cout<<"PASS overlapped client preserves header and message body\n";if(!run(true)){std::cerr<<"FAIL stalled service bound\n";return 2;}std::cout<<"PASS stalled service returns in bounded time without replay\n";return 0;}

