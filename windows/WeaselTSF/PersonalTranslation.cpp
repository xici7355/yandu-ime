// SPDX-License-Identifier: GPL-3.0-or-later
// Personal translation bridge. No network requests or disk text in the IME DLL.
#include "stdafx.h"
#include "WeaselTSF.h"
#include "EditSession.h"
#include <initguid.h>
#include <inputscope.h>
#include <vector>
#include <richedit.h>

namespace {
constexpr ULONG_PTR kPersonalMagic = 0x5054494d;
constexpr UINT kPublish = WM_APP + 271;
constexpr UINT kRefresh = WM_APP + 273;
constexpr UINT_PTR kLayoutRetry = 274;
constexpr UINT_PTR kEditWatch = 275;
constexpr UINT_PTR kFocusCheck = 276;
constexpr UINT_PTR kDeliveryRetry = 277;

bool FocusedDocument(ITfThreadMgr* manager, ITfContext* context) {
  com_ptr<ITfDocumentMgr> focused, owner;
  com_ptr<IUnknown> focusedIdentity, ownerIdentity;
  return context && SUCCEEDED(manager->GetFocus(&focused)) && focused &&
      SUCCEEDED(context->GetDocumentMgr(&owner)) && owner &&
      SUCCEEDED(focused->QueryInterface(&focusedIdentity)) &&
      SUCCEEDED(owner->QueryInterface(&ownerIdentity)) && focusedIdentity == ownerIdentity;
}

bool IsRichEdit(HWND window) {
  wchar_t name[256]{};
  GetClassNameW(window, name, 256);
  CharUpperBuffW(name, static_cast<DWORD>(wcslen(name)));
  return wcsstr(name, L"RICHEDIT") && !(GetWindowLongPtrW(window, GWL_STYLE) & ES_PASSWORD);
}

CHARRANGE EditSelection(HWND window) {
  CHARRANGE selection{-1, -1};
  SendMessageW(window, EM_EXGETSEL, 0, reinterpret_cast<LPARAM>(&selection));
  return selection;
}

bool ReadEdit(HWND window, LONG start, LONG length, std::wstring& text) {
  if (start < 0 || length < 0 || length > 12000) return false;
  std::vector<wchar_t> buffer(static_cast<size_t>(length) + 1);
  TEXTRANGEW range{{start, start + length}, buffer.data()};
  const auto count = SendMessageW(window, EM_GETTEXTRANGE, 0, reinterpret_cast<LPARAM>(&range));
  if (count != length) return false;
  text.assign(buffer.data(), length);
  return true;
}

// Diagnostic markers exist only in an explicitly isolated test process; no text.
void Trace(const std::wstring& marker) {
  wchar_t folder[MAX_PATH]{};
  if (!GetEnvironmentVariableW(L"PERSONAL_IME_TEST", nullptr, 0) ||
      !GetEnvironmentVariableW(L"PERSONAL_IME_TEST_USER_DIR", folder, MAX_PATH)) return;
  std::wstring path = std::wstring(folder) + L"\\bridge-trace.txt";
  HANDLE file = CreateFileW(path.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE,
      nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
  if (file == INVALID_HANDLE_VALUE) return;
  const std::wstring line = std::to_wstring(GetCurrentProcessId()) + L" " + marker + L"\r\n";
  DWORD written;
  WriteFile(file, line.data(), static_cast<DWORD>(line.size() * 2), &written, nullptr);
  CloseHandle(file);
}

bool IsSensitive(ITfContext* context, TfEditCookie ec, ITfRange* range) {
  TF_STATUS status{};
  if (FAILED(context->GetStatus(&status)) ||
      (status.dwDynamicFlags & TF_SD_READONLY))
    return true;
  com_ptr<ITfProperty> property;
  if (FAILED(context->GetProperty(GUID_PROP_INPUTSCOPE, &property)))
    return false;  // Hosts without scopes still have TSF keyboard-disabled checks.
  CComVariant value;
  if (FAILED(property->GetValue(ec, range, &value)) || value.vt != VT_UNKNOWN || !value.punkVal)
    return false;
  com_ptr<ITfInputScope> scope;
  if (FAILED(value.punkVal->QueryInterface(&scope))) return false;
  InputScope* scopes = nullptr;
  UINT count = 0;
  bool sensitive = false;
  if (SUCCEEDED(scope->GetInputScopes(&scopes, &count))) {
    for (UINT i = 0; i < count; ++i)
      if (scopes[i] == IS_PASSWORD || scopes[i] == IS_NUMERIC_PASSWORD)
        sensitive = true;
  }
  CoTaskMemFree(scopes);
  return sensitive;
}

bool AtEnd(ITfContext* context, TfEditCookie ec, ITfRange* range) {
  TF_SELECTION selection{};
  ULONG fetched = 0;
  if (FAILED(context->GetSelection(ec, TF_DEFAULT_SELECTION, 1, &selection, &fetched)) || fetched != 1)
    return false;
  com_ptr<ITfRange> selected;
  selected.Attach(selection.range);
  BOOL empty = FALSE;
  LONG compare = 1;
  return SUCCEEDED(selected->IsEmpty(ec, &empty)) && empty &&
         SUCCEEDED(selected->CompareEnd(ec, range, TF_ANCHOR_END, &compare)) && compare == 0;
}

bool ReadRange(TfEditCookie ec, ITfRange* range, std::wstring& text) {
  WCHAR data[12001];
  ULONG count = 0;
  if (FAILED(range->GetText(ec, 0, data, 12001, &count)) || count > 12000)
    return false;
  text.assign(data, count);
  return true;
}

LONG RangeEnd(ITfContext* context, TfEditCookie ec, ITfRange* range) {
  com_ptr<ITfRangeACP> acp;
  LONG start = 0, length = 0;
  if (SUCCEEDED(range->QueryInterface(&acp)) && SUCCEEDED(acp->GetExtent(&start, &length)))
    return start + length;
  // Anchor-based text stores need not implement ITfRangeACP. Count positions
  // using a halt bookmark, without reading any preceding document text.
  com_ptr<ITfRange> fromStart;
  LONG moved = 0;
  TF_HALTCOND halt{range, TF_ANCHOR_END, 0};
  if (FAILED(context->GetStart(ec, &fromStart)) ||
      FAILED(fromStart->ShiftStart(ec, LONG_MAX, &moved, &halt))) return -1;
  return moved;
}

bool Send(HWND receiver, HWND sender, const std::wstring& message, UINT timeout = 20) {
  COPYDATASTRUCT copy{kPersonalMagic, static_cast<DWORD>(message.size() * sizeof(wchar_t)),
                      const_cast<wchar_t*>(message.data())};
  DWORD_PTR result = 0;
  return SendMessageTimeoutW(receiver, WM_COPYDATA, reinterpret_cast<WPARAM>(sender),
                             reinterpret_cast<LPARAM>(&copy), SMTO_ABORTIFHUNG | SMTO_BLOCK,
                             timeout, &result) && result;
}

class PersonalReplaceSession : public CEditSession {
 public:
  PersonalReplaceSession(WeaselTSF* service, ITfContext* context,
                         unsigned long long revision, std::wstring expected,
                         std::wstring replacement)
      : CEditSession(service, context), revision_(revision),
        expected_(std::move(expected)), replacement_(std::move(replacement)) {}
  STDMETHODIMP DoEditSession(TfEditCookie ec) override {
    const auto result = _pTextService->PersonalReplace(_pContext, ec, revision_, expected_, replacement_);
    _pTextService->PersonalAcknowledge(revision_, SUCCEEDED(result));
    return result;
  }
 private:
  unsigned long long revision_;
  std::wstring expected_, replacement_;
};
class PersonalReadSession : public CEditSession {
 public:
  PersonalReadSession(WeaselTSF* service, ITfContext* context)
      : CEditSession(service, context) {}
  STDMETHODIMP DoEditSession(TfEditCookie ec) override {
    _pTextService->PersonalReadFinished();
    _pTextService->PersonalObserve(_pContext, ec);
    return S_OK;
  }
};
}  // namespace

void WeaselTSF::PersonalRefresh() {
  if (_personalWindow && _personalContext)
    PostMessageW(_personalWindow, kRefresh, 0, 0);
}

void WeaselTSF::PersonalFinishCommit() {
  if (_personalEdit) PersonalRefresh();
  if (_personalCommitPending) {
    _personalCommitEnded = true;
    PersonalRefresh();
  }
}

void WeaselTSF::PersonalFocusChanged() {
  if (_personalEdit && GetFocus() == _personalEdit && GetForegroundWindow() == _personalForeground)
    return;
  // Chromium and other TSF hosts may repeat focus callbacks for the same
  // document. Composition contexts may be pushed and popped in that document.
  // The exact source range and caret are still checked on each replacement.
  if (_personalContext && _personalForeground == GetForegroundWindow()) {
    if (FocusedDocument(_pThreadMgr, _personalContext))
      return;
    // A composition context can be popped before its document focus callback
    // catches up. Delay only the focus recheck, never relax write verification.
    SetTimer(_personalWindow, kFocusCheck, 60, nullptr);
    return;
  }
  PersonalReset();
}

void WeaselTSF::PersonalBeginComposition(ITfContext*) {
  const HWND focus = GetFocus();
  _personalBeginEdit = IsRichEdit(focus) ? focus : nullptr;
  if (_personalBeginEdit) {
    const auto selection = EditSelection(focus);
    _personalBeginStart = selection.cpMin;
    _personalBeginEnd = selection.cpMax;
    Trace(L"begin RichEdit start=" + std::to_wstring(selection.cpMin));
  }
}

bool WeaselTSF::PersonalObserveEdit() {
  if (!_personalEdit) return false;
  if (_personalReplacing) return true;
  if (GetFocus() != _personalEdit || GetForegroundWindow() != _personalForeground || !IsRichEdit(_personalEdit)) {
    PersonalReset();
    return true;
  }
  if (_status.composing) {
    _personalMessage = L"S|" + std::to_wstring(++_personalRevision) + L"|" +
        std::to_wstring(reinterpret_cast<UINT_PTR>(_personalWindow)) + L"|" +
        std::to_wstring(reinterpret_cast<UINT_PTR>(_personalForeground)) + L"|0|0|preedit\n" + _personalText;
    PostMessageW(_personalWindow, kPublish, 0, 0);
    return true;
  }
  const auto selection = EditSelection(_personalEdit);
  LONG end = _personalEditStart + static_cast<LONG>(_personalEditExpected.size());
  const LONG documentLength = GetWindowTextLengthW(_personalEdit);
  if (_personalEditLength > 0 && documentLength == _personalEditLength &&
      (selection.cpMin != end || selection.cpMax != end)) {
    PersonalReset();
    return true;
  }
  if (_personalEditLength > documentLength && selection.cpMin == selection.cpMax &&
      selection.cpMax == end - (_personalEditLength - documentLength) && selection.cpMax >= _personalEditStart) {
    const LONG count = selection.cpMax - _personalEditStart;
    std::wstring prefix;
    if (count > 0 && ReadEdit(_personalEdit, _personalEditStart, count, prefix) &&
        prefix == _personalEditExpected.substr(0, count)) {
      _personalEditExpected = prefix;
      end = selection.cpMax;
    } else {
      PersonalReset();
      return true;
    }
  }
  std::wstring actual;
  if (selection.cpMin != end || selection.cpMax != end ||
      !ReadEdit(_personalEdit, _personalEditStart, static_cast<LONG>(_personalEditExpected.size()), actual) ||
      actual != _personalEditExpected) {
    if (++_personalLayoutRetries <= 10) {
      ++_personalRevision;
      SetTimer(_personalWindow, kLayoutRetry, 30, nullptr);
    } else PersonalReset();
    return true;
  }
  _personalLayoutRetries = 0;
  KillTimer(_personalWindow, kLayoutRetry);
  _personalEditLength = GetWindowTextLengthW(_personalEdit);
  POINT caret{};
  if (!GetCaretPos(&caret) || !ClientToScreen(_personalEdit, &caret)) return true;
  if (actual != _personalText) ++_personalRevision;
  _personalText = actual;
  _personalMessage = L"S|" + std::to_wstring(_personalRevision) + L"|" +
      std::to_wstring(reinterpret_cast<UINT_PTR>(_personalWindow)) + L"|" +
      std::to_wstring(reinterpret_cast<UINT_PTR>(_personalForeground)) + L"|" +
      std::to_wstring(caret.x) + L"|" + std::to_wstring(caret.y + 20) + L"|text\n" + actual;
  PostMessageW(_personalWindow, kPublish, 0, 0);
  SetTimer(_personalWindow, kEditWatch, 100, nullptr);
  return true;
}

void WeaselTSF::PersonalCommitted(ITfContext* context, TfEditCookie ec, ITfRange* range,
                                 const std::wstring& text) {
  Trace(L"commit");
  if (_personalReplacing || _IsKeyboardDisabled() || IsSensitive(context, ec, range)) {
    Trace(L"commit disabled or sensitive");
    PersonalReset();
    return;
  }
  if (!_personalWindow) {
    // Builtin STATIC avoids a DLL-owned window class surviving DLL unload.
    _personalWindow = CreateWindowExW(0, L"STATIC", L"PersonalTranslateIme.Endpoint.v1", 0,
                                       0, 0, 0, 0, HWND_MESSAGE, nullptr, nullptr, nullptr);
    if (!_personalWindow) return;
    SetWindowLongPtrW(_personalWindow, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(this));
    SetWindowLongPtrW(_personalWindow, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(PersonalWindowProc));
    _personalRevision = GetTickCount64();
  }
  if (_personalBeginEdit && GetFocus() == _personalBeginEdit && IsRichEdit(_personalBeginEdit)) {
    const HWND edit = _personalBeginEdit;
    const LONG start = _personalBeginStart;
    const bool append = _personalEdit == edit && _personalBeginStart == _personalBeginEnd &&
        start == _personalEditStart + static_cast<LONG>(_personalEditExpected.size());
    const std::wstring expected = append ? _personalEditExpected + text : text;
    const LONG first = append ? _personalEditStart : start;
    PersonalReset();
    if (expected.empty() || expected.size() > 2000 || expected.find_first_of(L"\r\n") != std::wstring::npos) return;
    _personalEdit = edit;
    _personalEditStart = first;
    _personalEditExpected = expected;
    _personalContext = context;
    _personalForeground = GetForegroundWindow();
    range->Clone(&_personalRange);
    Trace(L"RichEdit commit expected length=" + std::to_wstring(expected.size()));
    PersonalRefresh();
    return;
  }
  // Chromium can reuse a context across DOM editors. Overlapping numeric
  // positions alone do not prove that this is the previous input field.
  // Verify the exact known prefix up to the new commit's start instead.
  bool append = false;
  if (_personalRange && _personalContext == context &&
      _personalForeground == GetForegroundWindow() && !_personalRecoveryExpected.empty()) {
    com_ptr<ITfRange> prefix;
    std::wstring actual;
    if (SUCCEEDED(_personalRange->Clone(&prefix)) &&
        SUCCEEDED(prefix->ShiftEndToRange(ec, range, TF_ANCHOR_START)) &&
        ReadRange(ec, prefix, actual) && actual == _personalRecoveryExpected)
      append = true;
  }
  if (append) {
    _personalRange->ShiftEndToRange(ec, range, TF_ANCHOR_END);
  } else {
    PersonalReset();
    range->Clone(&_personalRange);
    _personalContext = context;
  }
  if (_personalRange)
    _personalRange->SetGravity(ec, TF_GRAVITY_BACKWARD, TF_GRAVITY_FORWARD);
  _personalForeground = GetForegroundWindow();
  std::wstring committed;
  if (!ReadRange(ec, _personalRange, committed) || committed.size() > 2000 ||
      committed.find_first_of(L"\r\n") != std::wstring::npos) {
    PersonalReset();
  } else {
    _personalCommitExpected = committed;
    _personalRecoveryExpected = committed;
    _personalExpectedEnd = RangeEnd(context, ec, _personalRange);
    _personalCommitPending = true;
    _personalCommitEnded = false;
  }
}

void WeaselTSF::PersonalObserve(ITfContext* context, TfEditCookie ec) {
  Trace(L"observe range=" + std::to_wstring(!!_personalRange) + L" context=" + std::to_wstring(context == _personalContext));
  if (PersonalObserveEdit()) return;
  if (!_personalRange || context != _personalContext || _personalReplacing) return;
  if (_personalCommitPending && !_personalCommitEnded) return;
  if (_IsKeyboardDisabled() || IsSensitive(context, ec, _personalRange) ||
      _personalForeground != GetForegroundWindow()) {
    Trace(L"observe disabled/sensitive/focus");
    PersonalReset();
    return;
  }
  // Preedit is not sent. Keep the committed range until the next commit.
  if (_status.composing) {
    Trace(L"observe preedit");
    _personalMessage = L"S|" + std::to_wstring(++_personalRevision) + L"|" +
        std::to_wstring(reinterpret_cast<UINT_PTR>(_personalWindow)) + L"|" +
        std::to_wstring(reinterpret_cast<UINT_PTR>(_personalForeground)) + L"|0|0|preedit\n" + _personalText;
    PostMessageW(_personalWindow, kPublish, 0, 0);
    return;
  }
  if (!AtEnd(context, ec, _personalRange)) {
    Trace(L"observe caret moved");
    PersonalReset();
    return;
  }
  if (_personalCommitPending) {
    // RichEdit may replace the composition buffer when EndComposition returns,
    // collapsing retained ranges. Re-anchor only at the same end bookmark and
    // only when the exact known committed text is still immediately before it.
    com_ptr<ITfRange> committed;
    _personalRange->Clone(&committed);
    committed->Collapse(ec, TF_ANCHOR_END);
    LONG moved = 0;
    const LONG count = static_cast<LONG>(_personalCommitExpected.size());
    std::wstring actual;
    if (FAILED(committed->ShiftStart(ec, -count, &moved, nullptr)) || moved != -count ||
        !ReadRange(ec, committed, actual) || actual != _personalCommitExpected) {
      Trace(L"commit verification failed");
      if (++_personalLayoutRetries <= 10) {
        ++_personalRevision;
        SetTimer(_personalWindow, kLayoutRetry, 30, nullptr);
      } else PersonalReset();
      return;
    }
    _personalRange = committed;
    _personalRange->SetGravity(ec, TF_GRAVITY_BACKWARD, TF_GRAVITY_FORWARD);
    _personalCommitPending = false;
    _personalCommitEnded = false;
    _personalCommitExpected.clear();
  }
  std::wstring text;
  bool read = ReadRange(ec, _personalRange, text);
  Trace(L"range read=" + std::to_wstring(read) + L" length=" + std::to_wstring(text.size()) +
        L" end=" + std::to_wstring(RangeEnd(context, ec, _personalRange)) +
        L" expected=" + std::to_wstring(_personalExpectedEnd));
  if (read && text.empty() && !_personalRecoveryExpected.empty() &&
      _personalExpectedEnd >= 0 && RangeEnd(context, ec, _personalRange) == _personalExpectedEnd) {
    com_ptr<ITfRange> recovered;
    _personalRange->Clone(&recovered);
    recovered->Collapse(ec, TF_ANCHOR_END);
    LONG moved = 0;
    const LONG count = static_cast<LONG>(_personalRecoveryExpected.size());
    if (SUCCEEDED(recovered->ShiftStart(ec, -count, &moved, nullptr)) && moved == -count &&
        ReadRange(ec, recovered, text) && text == _personalRecoveryExpected) {
      _personalRange = recovered;
      _personalRange->SetGravity(ec, TF_GRAVITY_BACKWARD, TF_GRAVITY_FORWARD);
      Trace(L"recovered host anchor at unchanged ACP end");
    } else {
      text.clear();
    }
  }
  if (!read || text.empty()) {
    Trace(L"observe empty/invalid range");
    // Some stores briefly remove their IME buffer before reinserting the final
    // commit. Keep the bookmark across that transition, but invalidate its
    // revision immediately so no old replacement can be accepted.
    ++_personalRevision;
    if (++_personalLayoutRetries <= 10)
      SetTimer(_personalWindow, kLayoutRetry, 30, nullptr);
    else
      PersonalReset(false, read && text.empty() && _personalExpectedEnd > 0 && RangeEnd(context, ec, _personalRange) == 0);
    return;
  }
  _personalLayoutRetries = 0;
  KillTimer(_personalWindow, kLayoutRetry);
  com_ptr<ITfContextView> view;
  com_ptr<ITfRange> caret;
  RECT rect{};
  BOOL clipped = FALSE;
  _personalRange->Clone(&caret);
  caret->Collapse(ec, TF_ANCHOR_END);
  if (FAILED(context->GetActiveView(&view)) ||
      FAILED(view->GetTextExt(ec, caret, &rect, &clipped))) {
    Trace(L"observe no layout");
    return;  // Never display at a stale or guessed caret position.
  }
  Trace(L"observe snapshot length=" + std::to_wstring(text.size()));
  if (text != _personalText) ++_personalRevision;
  _personalText = text;
  _personalRecoveryExpected = text;
  _personalExpectedEnd = RangeEnd(context, ec, _personalRange);
  _personalMessage = L"S|" + std::to_wstring(_personalRevision) + L"|" +
      std::to_wstring(reinterpret_cast<UINT_PTR>(_personalWindow)) + L"|" +
      std::to_wstring(reinterpret_cast<UINT_PTR>(_personalForeground)) + L"|" +
      std::to_wstring(rect.left) + L"|" + std::to_wstring(rect.bottom) + L"|text\n" + text;
  PostMessageW(_personalWindow, kPublish, 0, 0);
  SetTimer(_personalWindow, kEditWatch, 300, nullptr);
}

void WeaselTSF::PersonalReset(bool inactive, bool detachedStore) {
  Trace(L"reset");
  if (_personalWindow) KillTimer(_personalWindow, kLayoutRetry);
  if (_personalWindow) KillTimer(_personalWindow, kEditWatch);
  if (_personalWindow) KillTimer(_personalWindow, kFocusCheck);
  if (_personalWindow) KillTimer(_personalWindow, kDeliveryRetry);
  _personalDeliveryRetries = 0;
  _personalReadPending = false;
  _personalEdit = nullptr;
  _personalEditLength = 0;
  _personalBeginEdit = nullptr;
  _personalEditExpected.clear();
  _personalLayoutRetries = 0;
  _personalRange.Release();
  _personalContext.Release();
  _personalText.clear();
  _personalCommitPending = false;
  _personalCommitEnded = false;
  _personalCommitExpected.clear();
  _personalRecoveryExpected.clear();
  _personalExpectedEnd = -1;
  ++_personalRevision;
  if (_personalWindow) {
    _personalMessage = L"S|" + std::to_wstring(_personalRevision) + L"|" +
        std::to_wstring(reinterpret_cast<UINT_PTR>(_personalWindow)) + L"|" +
        std::to_wstring(reinterpret_cast<UINT_PTR>(_personalForeground)) + L"|0|0|" + (inactive ? L"inactive\n" : (detachedStore ? L"detached\n" : L"clear\n"));
    PostMessageW(_personalWindow, kPublish, 0, 0);
  }
}

void WeaselTSF::PersonalShutdown() {
  PersonalReset(true);
  if (_personalWindow) {
    _personalAgent = FindWindowW(nullptr, L"PersonalTranslateIme.Agent.v1");
    if (_personalAgent) Send(_personalAgent, _personalWindow, _personalMessage);
    DestroyWindow(_personalWindow);
    _personalWindow = nullptr;
  }
}

HRESULT WeaselTSF::PersonalReplace(ITfContext* context, TfEditCookie ec,
                                   unsigned long long revision,
                                   const std::wstring& expected,
                                   const std::wstring& replacement) {
  if (_personalEdit) {
    const HWND edit = _personalEdit;
    const auto selection = EditSelection(edit);
    const LONG end = _personalEditStart + static_cast<LONG>(expected.size());
    std::wstring actual;
    if (revision != _personalRevision || _status.composing || GetFocus() != edit ||
        GetForegroundWindow() != _personalForeground || !IsRichEdit(edit) ||
        expected != _personalEditExpected || replacement.empty() || replacement.size() > 12000 ||
        selection.cpMin != end || selection.cpMax != end ||
        !ReadEdit(edit, _personalEditStart, static_cast<LONG>(expected.size()), actual) || actual != expected)
      return E_ABORT;
    _personalReplacing = true;
    CHARRANGE selected{_personalEditStart, end};
    SendMessageW(edit, EM_EXSETSEL, 0, reinterpret_cast<LPARAM>(&selected));
    SendMessageW(edit, EM_REPLACESEL, TRUE, reinterpret_cast<LPARAM>(replacement.c_str()));
    _personalReplacing = false;
    if (!ReadEdit(edit, _personalEditStart, static_cast<LONG>(replacement.size()), actual) || actual != replacement)
      return E_FAIL;
    _personalEditExpected = replacement;
    ++_personalRevision;
    PersonalObserveEdit();
    return S_OK;
  }
  if (!_personalRange || context != _personalContext || _status.composing ||
      !FocusedDocument(_pThreadMgr, context) ||
      revision != _personalRevision || _personalForeground != GetForegroundWindow() ||
      _IsKeyboardDisabled() || IsSensitive(context, ec, _personalRange) ||
      !AtEnd(context, ec, _personalRange))
    return E_ABORT;
  std::wstring actual;
  if (!ReadRange(ec, _personalRange, actual) || actual != expected || replacement.empty() || replacement.size() > 12000)
    return E_ABORT;
  _personalReplacing = true;
  auto result = _personalRange->SetText(ec, 0, replacement.data(), static_cast<LONG>(replacement.size()));
  if (SUCCEEDED(result)) {
    com_ptr<ITfRange> caret;
    _personalRange->Clone(&caret);
    caret->Collapse(ec, TF_ANCHOR_END);
    TF_SELECTION selection{caret, {TF_AE_NONE, FALSE}};
    result = context->SetSelection(ec, 1, &selection);
    // A successful SetText must be acknowledged even if host caret positioning fails.
    _personalText = replacement;
    _personalRecoveryExpected = replacement;
    _personalExpectedEnd = RangeEnd(context, ec, _personalRange);
    ++_personalRevision;
    result = S_OK;
  }
  _personalReplacing = false;
  PersonalObserve(context, ec);
  return result;
}

void WeaselTSF::PersonalAcknowledge(unsigned long long revision, bool success) {
  if (_personalAgent)
    Send(_personalAgent, _personalWindow, L"A|" + std::to_wstring(revision) + (success ? L"|1" : L"|0"), 20);
}

LRESULT CALLBACK WeaselTSF::PersonalWindowProc(HWND window, UINT message, WPARAM wp, LPARAM lp) {
  auto self = reinterpret_cast<WeaselTSF*>(GetWindowLongPtrW(window, GWLP_USERDATA));
  if (!self) return DefWindowProcW(window, message, wp, lp);
  if (message == WM_TIMER && wp == kFocusCheck) {
    KillTimer(window, kFocusCheck);
    if (self->_personalForeground == GetForegroundWindow() &&
        FocusedDocument(self->_pThreadMgr, self->_personalContext)) self->PersonalRefresh();
    else self->PersonalReset();
    return 0;
  }
  if (message == WM_TIMER && wp == kDeliveryRetry) {
    KillTimer(window, kDeliveryRetry);
    PostMessageW(window, kPublish, 0, 0);
    return 0;
  }
  if (message == WM_TIMER && wp == kLayoutRetry) {
    KillTimer(window, kLayoutRetry);
    self->PersonalRefresh();
    return 0;
  }
  if (message == WM_TIMER && wp == kEditWatch) {
    if (!self->_personalReplacing) {
      if (self->_personalEdit) self->PersonalObserveEdit();
      else self->PersonalRefresh();
    }
    return 0;
  }
  if (message == kRefresh && self->_personalContext) {
    if (self->_personalEdit) {
      self->PersonalObserveEdit();
      return 0;
    }
    if (self->_personalReadPending) return 0;
    self->_personalReadPending = true;
    auto session = new PersonalReadSession(self, self->_personalContext);
    HRESULT result;
    auto requested = self->_personalContext->RequestEditSession(self->_tfClientId, session,
        TF_ES_ASYNC | TF_ES_READ, &result);
    if (FAILED(requested) || FAILED(result)) self->_personalReadPending = false;
    session->Release();
    return 0;
  }
  if (message == kPublish) {
    self->_personalAgent = FindWindowW(nullptr, L"PersonalTranslateIme.Agent.v1");
    Trace(L"publish length=" + std::to_wstring(self->_personalMessage.size()));
    Trace(L"publish agent=" + std::to_wstring(!!self->_personalAgent));
    bool delivered = self->_personalAgent && Send(self->_personalAgent, window, self->_personalMessage);
    Trace(L"send=" + std::to_wstring(delivered));
    if (delivered) {self->_personalDeliveryRetries = 0; KillTimer(window, kDeliveryRetry);}
    else if (++self->_personalDeliveryRetries <= 5) SetTimer(window, kDeliveryRetry, 50, nullptr);
    return 0;
  }
  if (message == WM_COPYDATA && self->_personalContext && reinterpret_cast<HWND>(wp) == self->_personalAgent) {
    auto data = reinterpret_cast<COPYDATASTRUCT*>(lp);
    if (!data || data->dwData != kPersonalMagic || data->cbData > 65536 || data->cbData % 2 || !data->lpData)
      return FALSE;
    std::wstring command(static_cast<wchar_t*>(data->lpData), data->cbData / 2);
    auto newline = command.find(L'\n');
    if (newline == std::wstring::npos) return FALSE;
    unsigned long long revision = 0;
    unsigned expected = 0, replacement = 0;
    if (swscanf_s(command.substr(0, newline).c_str(), L"R|%llu|%u|%u", &revision, &expected, &replacement) != 3 ||
        expected > 12000 || replacement > 12000 || newline + 1 + expected + replacement != command.size())
      return FALSE;
    if (self->_personalEdit) {
      const auto result = self->PersonalReplace(self->_personalContext, 0, revision,
          command.substr(newline + 1, expected), command.substr(newline + 1 + expected, replacement));
      self->PersonalAcknowledge(revision, SUCCEEDED(result));
      return TRUE;
    }
    auto session = new PersonalReplaceSession(self, self->_personalContext, revision,
        command.substr(newline + 1, expected), command.substr(newline + 1 + expected, replacement));
    HRESULT result;
    auto hr = self->_personalContext->RequestEditSession(self->_tfClientId, session,
        TF_ES_ASYNC | TF_ES_READWRITE, &result);
    session->Release();
    if (FAILED(hr) || FAILED(result)) self->PersonalAcknowledge(revision, false);
    return SUCCEEDED(hr) && SUCCEEDED(result);
  }
  return DefWindowProcW(window, message, wp, lp);
}



