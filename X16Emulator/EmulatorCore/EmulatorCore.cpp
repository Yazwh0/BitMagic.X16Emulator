// EmulatorCode.cpp : Defines the exported functions for the DLL.
//
#include "pch.h"
#include "framework.h"
#include "EmulatorCore.h"
#include <cstdint>
#include <chrono>
#include <thread>
#include "ym_wrapper.cpp"
#include "../../../External/ymfm/src/ymfm_opm.cpp"


// This is an example of an exported variable
//EMULATORCODE_API int nEmulatorCode=0;

void sleep(__int64 usec);
__int64 get_ticks();
void step_ym();
void write_register_ym();

extern "C" 
{
    int __fastcall asm_func(state* state);
    ym_wrapper* _ym;
    bool _initialised = false;

    // This is an example of an exported function.
    // int8_t* mainMemory,
    EMULATORCODE_API int fnEmulatorCode(state* state)
    {
        if (!_initialised)
        {   
            _ym = new ym_wrapper(state);
            state->step_ym = &step_ym;
            state->write_register_ym = &write_register_ym;
            state->sleep = &sleep;
            state->get_ticks = &get_ticks;
            _initialised = true;
        }
        return asm_func(state);
    }
}

void __fastcall step_ym()
{
    _ym->step();
}

void __fastcall write_register_ym()
{
    _ym->write_register();
}

void __fastcall sleep(__int64 usec)
{
    std::this_thread::sleep_for(std::chrono::microseconds(usec));
}

__int64 __fastcall get_ticks()
{
    auto duration = std::chrono::system_clock::now().time_since_epoch();
    return std::chrono::duration_cast<std::chrono::milliseconds>(duration).count();
}


// This is the constructor of a class that has been exported.
//CEmulatorCode::CEmulatorCode()
//{
//    return;
//}
//
//int CEmulatorCode::TestFunc()
//{
//    
//
//    return 5;
//}