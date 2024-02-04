#include <stdint.h>
#include <unistd.h>
#include <sys/time.h>
#include <signal.h>
#include "state.h"
#include "ym_wrapper.cpp"
#include "../../../External/ymfm/src/ymfm_opm.cpp"

extern int64_t asm_func(void *state);

extern "C"
{
    void sleepWrapper(int64_t usec);
    int64_t getTicks();
    void step_ym();
    void write_register_ym();

    ym_wrapper* _ym;
    bool _initialised = false;

    int32_t fnEmulatorCode(void* state)
    {
        int32_t toReturn = 0;

        struct state* actState = (struct state*)state;

        if (!_initialised)
        {
            _ym = new ym_wrapper(actState);
            _initialised = true;
        }

        actState->GetTicks = &getTicks;
        actState->Sleep = &sleepWrapper;
        actState->step_ym = &step_ym;
        actState->write_register_ym = &write_register_ym;

        __asm__ __volatile__(
            "mov %1, %%rsi    \t\n"
            "call asm_func    \t\n"
            "mov %%eax, %0    \t\n" : "=a"(toReturn) : "m"(actState): "rcx", "rdx", "rdi", "r8", "r9", "r10", "r11");

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

    void step_ym()
    {
        _ym->step();
    }

    void write_register_ym()
    {
        _ym->write_register();
    }
}