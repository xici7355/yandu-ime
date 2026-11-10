#pragma once

#define WEASEL_CODE_NAME "PersonalTranslateIme"
#define WEASEL_REG_KEY L"Software\\PersonalTranslateIme\\Weasel"
#define RIME_REG_KEY L"Software\\PersonalTranslateIme"

#define STRINGIZE(x) #x
#define VERSION_STR(x) STRINGIZE(x)
#define WEASEL_VERSION VERSION_STR(VERSION_MAJOR.VERSION_MINOR.VERSION_PATCH)
#define WEASEL_PRODUCT_VERSION VERSION_STR(PRODUCT_VERSION)
