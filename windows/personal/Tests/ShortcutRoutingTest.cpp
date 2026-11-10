#include <windows.h>
#include <msctf.h>
#include <iostream>
int wmain(int argc,wchar_t** argv){if(argc!=2)return 2;CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);auto lib=LoadLibraryW(argv[1]);if(!lib){std::cerr<<"load error="<<GetLastError()<<"\n";return 3;}
 auto get=reinterpret_cast<HRESULT(STDAPICALLTYPE*)(REFCLSID,REFIID,void**)>(GetProcAddress(lib,"DllGetClassObject"));const CLSID clsid={0x54de72b1,0x0769,0x4ef9,{0x9c,0xa6,0x7b,0x4d,0x0d,0xe6,0xcb,0x0a}};IClassFactory* factory=nullptr;ITfKeystrokeMgr* unused=nullptr;(void)unused;ITfKeyEventSink* sink=nullptr;
 if(!get||FAILED(get(clsid,IID_IClassFactory,reinterpret_cast<void**>(&factory))))return 4;if(FAILED(factory->CreateInstance(nullptr,IID_ITfKeyEventSink,reinterpret_cast<void**>(&sink))))return 5;factory->Release();
 BYTE saved[256]{};GetKeyboardState(saved);int checks=0;
 struct Case{BYTE modifier;WPARAM key;const char* name;};Case cases[]={{VK_MENU,'A',"Alt+A"},{VK_CONTROL,'C',"Ctrl+C"},{VK_CONTROL,VK_RETURN,"Ctrl+Enter"},{VK_LWIN,VK_SPACE,"Win+Space"},{0,VK_MENU,"Alt key"},{0,VK_CONTROL,"Ctrl key"},{0,VK_LWIN,"Windows key"}};
 for(const auto& c:cases){BYTE state[256]{};if(c.modifier)state[c.modifier]=0x80;SetKeyboardState(state);BOOL eaten=TRUE;HRESULT hr=sink->OnTestKeyDown(nullptr,c.key,0,&eaten);if(FAILED(hr)||eaten)return 10;hr=sink->OnKeyDown(nullptr,c.key,0,&eaten);if(FAILED(hr)||eaten)return 11;hr=sink->OnTestKeyUp(nullptr,c.key,0,&eaten);if(FAILED(hr)||eaten)return 12;hr=sink->OnKeyUp(nullptr,c.key,0,&eaten);if(FAILED(hr)||eaten)return 13;std::cout<<"PASS "<<c.name<<" bypasses all four IME key callbacks\n";checks+=4;}
 SetKeyboardState(saved);sink->Release();FreeLibrary(lib);CoUninitialize();std::cout<<"OK "<<checks<<" shortcut routing checks without registration\n";return 0;}
