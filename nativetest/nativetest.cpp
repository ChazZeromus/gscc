// nativetest.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include <stdlib.h>
#include <Windows.h>

signed int useless(float number) {
	//assert(sizeof(float) == sizeof(unsigned int));

	unsigned int f32 = *reinterpret_cast<unsigned int*>(&number);

	signed char exp = -127 + ((f32 >> 23) & 0xFF);

	unsigned int whole = f32 & 0x007FFFFF;

	int shift = 23 - exp;

	if (exp > 0) {
		whole >>= shift;
	}

	whole |= (1 << exp);

	if (f32 & 0x80000000) {
		whole = ~whole + 1;
	}

	return (signed int)whole;
}

int _tmain(int argc, _TCHAR* argv[])
{
	auto blah = useless(-65355.212); // 1151709217
	int a = 123, b = 456, *c = &a;

	printf("%d", c + 2);

	return 0;
}

