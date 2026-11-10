#include <windows.h>
#include <filesystem>
#include <iostream>
#include <string>
#include "rime_api.h"
int wmain(int argc, wchar_t** argv) {
 if (argc != 4) return 2;
 auto module=LoadLibraryW(argv[1]);
 if(!module) {std::cerr<<"load library failed "<<GetLastError()<<"\n";return 3;}
 auto get=reinterpret_cast<RimeApi*(*)()>(GetProcAddress(module,"rime_get_api"));
 if(!get)return 4;
 auto api=get();
 auto utf8=[](const wchar_t* s){int n=WideCharToMultiByte(CP_UTF8,0,s,-1,nullptr,0,nullptr,nullptr);std::string r(n,0);WideCharToMultiByte(CP_UTF8,0,s,-1,r.data(),n,nullptr,nullptr);r.pop_back();return r;};
 auto shared=utf8(argv[2]),user=utf8(argv[3]);std::filesystem::create_directories(argv[3]);
 RIME_STRUCT(RimeTraits,traits);traits.shared_data_dir=shared.c_str();traits.user_data_dir=user.c_str();traits.app_name="rime.personal-test";
 api->setup(&traits);api->initialize(&traits);
 if(api->start_maintenance(True))api->join_maintenance_thread();
 auto id=api->create_session();if(!id)return 5;
 if(!api->select_schema(id,"luna_pinyin_simp")){std::cerr<<"select schema failed\n";return 6;}
 for(char key:std::string("nihao"))api->process_key(id,key,0);
 RIME_STRUCT(RimeContext,context);if(!api->get_context(id,&context))return 7;
 int selected=-1;
 for(int i=0;i<context.menu.num_candidates;++i){if(std::string(context.menu.candidates[i].text)==u8"你好")selected=i;}
 api->free_context(&context);
 if(selected<0){std::cerr<<"missing nihao candidate\n";return 8;}
 api->select_candidate(id,selected);
 RIME_STRUCT(RimeCommit,commit);if(!api->get_commit(id,&commit))return 9;
 bool ok=std::string(commit.text)==u8"你好";api->free_commit(&commit);
 if(!ok)return 10;
 std::cout<<"PASS full-pinyin candidate and Chinese commit\n";
 api->set_option(id,"ascii_mode",True);
 bool eaten=api->process_key(id,'a',0);
 ok=!eaten; // ASCII mode normally lets the host handle the physical key.
 if(eaten && api->get_commit(id,&commit)){ok=std::string(commit.text)=="a";api->free_commit(&commit);}
 if(!ok)return 12;
 std::cout<<"PASS English passthrough\n";
 api->set_option(id,"ascii_mode",False);
 struct Sample {const char* code;const char* expected;};
 Sample samples[]={
  {"jintian",u8"今天"},{"mingtian",u8"明天"},{"weixin",u8"微信"},
  {"shurufa",u8"输入法"},{"rengongzhineng",u8"人工智能"},{"dashuju",u8"大数据"},
  {"yunjisuan",u8"云计算"},{"shipinhuiyi",u8"视频会议"},{"shishifanyi",u8"实时翻译"},
  {"mimachongzhi",u8"密码重置"},{"shujuku",u8"数据库"},{"xitonggengxin",u8"系统更新"},
  {"gongzuojihua",u8"工作计划"},{"anzhuozhuomian",u8"安卓桌面"},
  {"jintiantianqihenhao",u8"今天天气很好"},{"mingtianxiawusandiankaihui",u8"明天下午三点开会"},
  {"qingbangwofanyiyixia",u8"请帮我翻译一下"},{"zhegewenjianxuyaoxiugai",u8"这个文件需要修改"},
  {"douyin",u8"抖音"},{"duanshipin",u8"短视频"},{"yuanchengzhuomian",u8"远程桌面"},
  {"kaifazhe",u8"开发者"},{"kaiyuanxiangmu",u8"开源项目"},
  {"wangluoanquan",u8"网络安全"},{"jiekoudizhi",u8"接口地址"},{"jiantieban",u8"剪贴板"},
  {"xuniji",u8"虚拟机"},{"fanyihuancun",u8"翻译缓存"}
 };
 int first=0,topFive=0;
 for(const auto& sample:samples){
  api->clear_composition(id);for(const char* c=sample.code;*c;++c)api->process_key(id,*c,0);
  RIME_STRUCT(RimeContext,current);if(!api->get_context(id,&current))return 13;
  int rank=0;for(int i=0;i<current.menu.num_candidates&&i<5;++i)if(std::string(current.menu.candidates[i].text)==sample.expected)rank=i+1;
  if(rank==1)++first;if(rank>0)++topFive;
  std::cout<<"CASE "<<sample.code<<" rank="<<rank<<" first="<<(current.menu.num_candidates?current.menu.candidates[0].text:"")<<"\n";
  api->free_context(&current);
 }
 std::cout<<"BENCHMARK first="<<first<<" top5="<<topFive<<" total="<<(sizeof(samples)/sizeof(samples[0]))<<"\n";
 api->destroy_session(id);api->finalize();FreeLibrary(module);std::cout<<"OK native engine smoke\n";return 0;
}
