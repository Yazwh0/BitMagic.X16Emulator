// The following ifdef block is the standard way of creating macros which make exporting
// from a DLL simpler. All files within this DLL are compiled with the EMULATORCODE_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see
// EMULATORCODE_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef EMULATORCODE_EXPORTS
#define EMULATORCODE_API __declspec(dllexport)
#else
#define EMULATORCODE_API __declspec(dllimport)
#endif
#include <cstdint>

// This class is exported from the dll
//class EMULATORCODE_API CEmulatorCode {
//public:
//	CEmulatorCode(void);
//	int TestFunc();
//	// TODO: add your methods here.
//};


//extern EMULATORCODE_API int nEmulatorCode;

#ifndef STATE_DEF
#define STATE_DEF

extern "C" 
{
	struct state 
	{
		__int64 (* get_ticks) ();
		void (* sleep) (__int64);
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

		uint32_t initialised;
		uint32_t blank;

		__int64 wrapper_flags; // used by linux wrapper

		int8_t* memory;
		//int8_t* rom_ptr;
		//int8_t* rambank_ptr;
		//int8_t* vram_ptr;

		//uint64_t data0_address;
		//uint64_t data1_address;
		//uint64_t data0_step;
		//uint64_t data1_step;

		//uint64_t clock;
		//uint16_t pc;
		//uint16_t stackpointer;
		//uint8_t a;
		//uint8_t x;
		//uint8_t y;

		//bool decimal;
		//bool breakFlag;
		//bool overflow;
		//bool negative;
		//bool carry;
		//bool zero;
		//bool interruptDisable;
		//bool interrupt;

		//bool addrsel;
		//bool dcsel;
		//uint8_t dc_hscale;
		//uint8_t dc_vscale;
		//uint8_t dc_border;
		//uint8_t dc_hstart;
		//uint8_t dc_hstop;
		//uint8_t dc_vstart;
		//uint8_t dc_vstop;

		//bool spriteEnable;
		//bool layer0Enable;
		//bool layer1Enable;

		//uint32_t layer0_mapAddress;
		//uint32_t layer0_tileAddress;
		//uint16_t layer0_hscroll;
		//uint16_t layer0_vscroll;
		//uint8_t layer0_mapHeight;
		//uint8_t layer0_mapWidth;
		//bool layer0_bitmapMode;
		//uint8_t layer0_colourDepth;
		//uint8_t layer0_tileHeight;
		//uint8_t layer0_tileWidth;

		//uint32_t layer1_mapAddress;
		//uint32_t layer1_tileAddress;
		//uint16_t layer1_hscroll;
		//uint16_t layer1_vscroll;
		//uint8_t layer1_mapHeight;
		//uint8_t layer1_mapWidth;
		//bool layer1_bitmapMode;
		//uint8_t layer1_colourDepth;
		//uint8_t layer1_tileHeight;
		//uint8_t layer1_tileWidth;

		//uint16_t interrupt_linenum;
		//bool interrupt_aflow;
		//bool interrupt_spcol;
		//bool interrupt_line;
		//bool interrupt_vsync;
	};

	EMULATORCODE_API int fnEmulatorCode(state* state);
}
#endif