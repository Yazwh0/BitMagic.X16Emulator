#include <stdint.h>
#include <unistd.h>
#include <sys/time.h>
#include <signal.h>

extern int64_t asm_func(void *state);

struct EmulatorState
{
    int64_t (* GetTicks)();
    void (* Sleep)(int64_t);
    void (* step_ym)();
    void (* write_register_ym)();
    uint32_t ym_timer0;
    uint32_t ym_timer1;
    uint32_t ym_busy_timer;
    uint32_t ym_interrupt;
    uint32_t ym_address;
    uint32_t ym_data;
    int32_t ym_left;
    int32_t ym_right;
    int64_t WrapperFlags;
};

void sleepWrapper(int64_t usec);
int64_t getTicks();

int32_t fnEmulatorCode(void* state)
{
    int32_t toReturn = 0;

    struct EmulatorState* actState = state;

    actState->GetTicks = &getTicks;
    actState->Sleep = &sleepWrapper;

    __asm__ __volatile__(
        "mov %%rdi, %%rsi \t\n"
        "call asm_func    \t\n"
        "mov %%eax, %0    \t\n" : "=a"(toReturn) : :);

    return toReturn;
}

void sleepWrapper(int64_t usec)
{
    sleep(usec);
}

int64_t getTicks()
{
    struct timeval tp;
    gettimeofday(&tp, NULL);
    int64_t ms = tp.tv_sec * 1000 + tp.tv_usec / 1000;

    return ms;
}

