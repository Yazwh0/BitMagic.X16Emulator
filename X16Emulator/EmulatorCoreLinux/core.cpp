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

        // asm_func (Core.asm) reads its state-pointer argument out of rcx -- matching the
        // Windows x64 ABI it's built against, not the SysV rdi/rsi convention this
        // translation unit otherwise uses. core.obj is one binary shared verbatim between
        // Windows and Linux (see build.sh), so this hand-rolled bridge has to stage the
        // pointer into that exact register rather than relying on either platform's normal
        // calling convention.
        //
        // The push/and/mov-rbp dance forces 16-byte stack alignment at the call site: SysV
        // requires rsp % 16 == 0 immediately before a call, but a `call` hidden inside an
        // opaque asm string gets no such guarantee from GCC (it only maintains that
        // invariant for calls it emits itself), especially on this hot path where the
        // function otherwise does almost nothing and may get a minimal prologue.
        // %1 ("m"(actState)) must be dereferenced before rbp is touched below: GCC's
        // compile-time address for a memory operand is very likely rbp-relative, and once
        // "mov %%rsp, %%rbp" repoints rbp at our saved-rsp slot instead of the function's
        // real frame, that same addressing expression would read garbage instead of
        // actState.
        __asm__ __volatile__(
            "mov %1, %%rcx    \t\n"
            "push %%rbp       \t\n"
            "mov %%rsp, %%rbp \t\n"
            "and $-16, %%rsp  \t\n"
            "call asm_func    \t\n"
            "mov %%rbp, %%rsp \t\n"
            "pop %%rbp        \t\n"
            "mov %%eax, %0    \t\n" : "=a"(toReturn) : "m"(actState): "rcx", "rdx", "rdi", "rsi", "r8", "r9", "r10", "r11", "memory");

        return toReturn;
    }

    void sleepWrapper(int64_t usec)
    {
        // Core.asm calls state.sleep with the microsecond count staged into rcx (see the
        // "mov rcx, rbx" right before its call), matching the Windows x64 ABI's first-arg
        // register rather than SysV's rdi -- so grab the real value out of rcx here rather
        // than trusting the compiler's normal SysV prologue for this parameter. This was
        // previously reading rbx, which isn't where the caller puts it either.
        __asm__ __volatile__(
            "mov %%rcx, %0" : "=m"(usec) :
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
