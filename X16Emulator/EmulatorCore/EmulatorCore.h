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
	struct zimodem
	{
		// Mirrors zimodem_state in zimodem.asm field-for-field (order + size, not names --
		// see that file for the write_serial/tx_byte/rx_available/rx_byte contract).
		void (* write_serial)();
		uint8_t tx_byte;
		uint8_t rx_available;
		uint8_t rx_byte;
	};

	struct uart
	{
		zimodem* zimodem;
	};

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

		uart* uart;
		__int64 clock_uart;    // MUST match State.asm / CpuState.cs -- without it, `memory`
		                       // lands 8 bytes low and ym_wrapper writes through a null ptr

		int8_t* memory;
	};

	EMULATORCODE_API int fnEmulatorCode(state* state);
}
#endif