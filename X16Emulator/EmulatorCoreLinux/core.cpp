#include <stdint.h>
#include <unistd.h>
#include <chrono>
#include <thread>
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

    int32_t fnEmulatorCode(void* state)
    {
        int32_t toReturn = 0;

        struct state* actState = (struct state*)state;

        if (actState->initialised == 0)
        {
            _ym = new ym_wrapper(actState);
            actState->GetTicks = &getTicks;
            actState->Sleep = &sleepWrapper;
            actState->step_ym = &step_ym;
            actState->write_register_ym = &write_register_ym;
            actState->initialised = 1;
        }

        __asm__ __volatile__(
            "mov %1, %%rsi    \t\n"
            "call asm_func    \t\n"
            "mov %%eax, %0    \t\n" : "=a"(toReturn) : "m"(actState): "rcx", "rdx", "rdi", "r8", "r9", "r10", "r11");

        return toReturn;
    }

    void sleepWrapper(int64_t usec)
    {
        __asm__ __volatile__(
            "mov %%rbx, %0" : : "m"(usec)
        );

        std::this_thread::sleep_for(std::chrono::microseconds(usec));
    }

    int64_t getTicks()
    {
        auto duration = std::chrono::system_clock::now().time_since_epoch();
        auto ticks = std::chrono::duration_cast<std::chrono::milliseconds>(duration).count();

        __asm__ __volatile__(
            "mov %%rax, %0" : "=a"(ticks)
        );

        return ticks;
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
