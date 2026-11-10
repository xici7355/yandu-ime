#include <windows.h>
#include <filesystem>
#include <iostream>
#include <string>
#include <fstream>
#include "rime_api.h"
static std::string utf8(const wchar_t* s){int n=WideCharToMultiByte(CP_UTF8,0,s,-1,nullptr,0,nullptr,nullptr);std::string r(n,0);WideCharToMultiByte(CP_UTF8,0,s,-1,r.data(),n,nullptr,nullptr);r.pop_back();return r;}
static bool choose(RimeApi* api,RimeSessionId id,const char* text){RimeCandidateListIterator it{};int index=-1;if(api->candidate_list_begin(id,&it)){int n=0;while(n++<3000&&api->candidate_list_next(&it)){if(std::string(it.candidate.text)==text){index=it.index;break;}}api->candidate_list_end(&it);}return index>=0&&api->select_candidate(id,index);}
static std::string first(RimeApi* api,RimeSessionId id){RIME_STRUCT(RimeContext,c);if(!api->get_context(id,&c))return "";std::string s=c.menu.num_candidates?c.menu.candidates[0].text:"";api->free_context(&c);return s;}
int wmain(int argc,wchar_t** argv){if(argc<4)return 2;bool legacy=argc>=5&&std::wstring(argv[4])==L"--train-legacy";bool checkOnly=argc>=5&&std::wstring(argv[4])==L"--check-only";SetConsoleOutputCP(CP_UTF8);auto lib=LoadLibraryW(argv[1]);if(!lib)return 3;auto api=reinterpret_cast<RimeApi*(*)()>(GetProcAddress(lib,"rime_get_api"))();auto shared=utf8(argv[2]),user=utf8(argv[3]);std::filesystem::create_directories(argv[3]);RIME_STRUCT(RimeTraits,traits);traits.shared_data_dir=shared.c_str();traits.user_data_dir=user.c_str();traits.app_name="rime.yandu-learning-test";api->setup(&traits);api->initialize(&traits);if(api->start_maintenance(True))api->join_maintenance_thread();
 if(!legacy&&!checkOnly){if(!api->deploy_config_file("weasel.yaml","config_version")){std::cerr<<"FAIL frontend deployment\n";return 4;}
 RimeConfig style{};Bool horizontal=False;if(!api->config_open("weasel",&style)||!api->config_get_bool(&style,"style/horizontal",&horizontal)||!horizontal){std::cerr<<"FAIL actual frontend horizontal configuration\n";return 5;}char scheme[128]{};api->config_get_string(&style,"style/color_scheme",scheme,sizeof(scheme));api->config_close(&style);if(std::string(scheme)!="yandu_light")return 6;std::cout<<"PASS deployed horizontal frontend and yandu palette\n";}
 auto id=api->create_session();if(!api->select_schema(id,"luna_pinyin_simp"))return 7;
 const std::string code="yanduxinghejihua";const std::string target=u8"言渡星河计划";if(checkOnly){for(char c:code)api->process_key(id,c,0);bool valid=first(api,id)==target;api->destroy_session(id);api->finalize();std::cout<<(valid?"PASS":"FAIL")<<" learned phrase survives V1 to V3 dictionary migration\n";return valid?0:21;}for(char c:code)api->process_key(id,c,0);auto before=first(api,id);std::cout<<"before="<<before<<"\n";
 if(!choose(api,id,u8"言")||!choose(api,id,u8"渡")||!choose(api,id,u8"星河")||!choose(api,id,u8"计划")){std::cerr<<"FAIL manual combination selection\n";return 8;}
 RIME_STRUCT(RimeCommit,commit);if(!api->get_commit(id,&commit))return 9;bool committed=std::string(commit.text)==target;api->free_commit(&commit);if(!committed)return 10;std::cout<<"PASS manually assembled new combination\n";
 for(char c:code)api->process_key(id,c,0);auto second=first(api,id);std::cout<<"second="<<second<<"\n";if(second!=target){std::cerr<<"FAIL second entry learning\n";return 11;}std::cout<<"PASS learned combination becomes first candidate on second entry\n";
 api->clear_composition(id);api->destroy_session(id);api->finalize();api->initialize(&traits);if(api->start_maintenance(False))api->join_maintenance_thread();id=api->create_session();if(!api->select_schema(id,"luna_pinyin_simp"))return 12;for(char c:code)api->process_key(id,c,0);auto restart=first(api,id);if(restart!=target){std::cerr<<"FAIL learned phrase restart persistence\n";return 13;}std::cout<<"PASS learned phrase persists after engine restart\n";if(legacy){api->destroy_session(id);api->finalize();return 0;}
 api->clear_composition(id);
 auto commitPiece=[&](const std::string& code,const char* text){for(char c:code)api->process_key(id,c,0);if(!choose(api,id,text))return false;RIME_STRUCT(RimeCommit,piece);if(!api->get_commit(id,&piece))return false;bool valid=std::string(piece.text)==text;api->free_commit(&piece);return valid;};
 // Word-history learning is distinct from learning one composition assembled
 // through several candidate selections. Exercise separate committed words.
 bool pieces=commitPiece("xinghe",u8"星河")&&commitPiece("songlin",u8"松林")&&commitPiece("fangan",u8"方案");
 if(!pieces){std::cerr<<"FAIL separate word commit fixture\n";return 14;}
 for(char c:std::string("xinghesonglinfangan"))api->process_key(id,c,0);auto history=first(api,id);std::cout<<"separateCommitNext="<<history<<"\n";
 std::cout<<(history==u8"星河松林方案"?"PASS":"INFO")<<" composition of separately committed words (not proof of storage)\n";api->clear_composition(id);if(api->sync_user_data())api->join_maintenance_thread();bool historyStored=false;for(const auto& f:std::filesystem::recursive_directory_iterator(argv[3])){if(f.is_regular_file()&&f.path().filename().wstring().find(L"userdb.txt")!=std::wstring::npos){std::ifstream in(f.path());std::string line;while(std::getline(in,line))if(line.find(u8"星河松林方案")!=std::string::npos)historyStored=true;}}std::cout<<"separateCommitStored="<<historyStored<<"\n";
 api->destroy_session(id);api->finalize();FreeLibrary(lib);std::cout<<"OK learning and actual frontend configuration\n";return 0;}
