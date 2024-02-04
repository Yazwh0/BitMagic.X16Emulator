#include <cstdint>

#ifndef EMU_STATE
#define EMU_STATE

struct state
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
 	int8_t* memory;	
};

#endif