#include <stdint.h>

extern int64_t asm_func(void *state);

int64_t fmEmulatorCode(void* state)
{
    int64_t toReturn = 0;

    __asm__ __volatile__(
        "mov %%rdi, %%rsi \t\n"
        "call asm_fund    \t\n"
        "mov %%rax, %0    \t\n" : "=a"(toReturn) : :);

    return toReturn;
}
