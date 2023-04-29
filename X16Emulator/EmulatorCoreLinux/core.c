#include <stdint.h>

extern int64_t asm_func(void *state);

int32_t fnEmulatorCode(void* state)
{
    int32_t toReturn = 0;

    __asm__ __volatile__(
        "mov %%rdi, %%rsi \t\n"
        "call asm_fund    \t\n"
        "mov %%eax, %0    \t\n" : "=a"(toReturn) : :);

    return toReturn;
}
