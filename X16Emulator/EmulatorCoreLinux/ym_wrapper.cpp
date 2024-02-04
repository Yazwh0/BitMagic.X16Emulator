#include "../../../External/ymfm/src/ymfm_opm.h"

#ifndef YM_WRAPPER
#define YM_WRAPPER

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

class ym_wrapper : public ymfm::ymfm_interface
{
private:
	ymfm::ym2151 _ym_emulator;
	ymfm::ym2151::output_data _ym_output;
	state* _emulator_state;
public:
	explicit ym_wrapper(state* emulator_state):
		_ym_emulator(*this),
		_emulator_state(emulator_state)
	{
	}

	void ymfm_set_timer(uint32_t tnum, int32_t duration_in_clocks) override {
		if (tnum >= 2) return;
		if (tnum == 0)
			_emulator_state->ym_timer0 = duration_in_clocks;
		else
			_emulator_state->ym_timer1 = duration_in_clocks;
	}

	void ymfm_set_busy_end(uint32_t clocks) override {
		_emulator_state->ym_busy_timer = clocks;
	}

	bool ymfm_is_busy() override {
		return _emulator_state->ym_busy_timer > 0;
	}

	void ymfm_update_irq(bool asserted) override {
		_emulator_state->ym_interrupt = asserted;
	}
	
	// should be called every 64 clocks of the YM chip. or ~143 CPU cycles
	void step()
	{
		if (_emulator_state->ym_busy_timer != 0)
			_emulator_state->ym_busy_timer = _emulator_state->ym_busy_timer - 64 > 0 ? _emulator_state->ym_busy_timer - 64 : 0;

		if (_emulator_state->ym_timer0 != 0)
		{
			_emulator_state->ym_timer0 = _emulator_state->ym_timer0 - 64 > 0 ? _emulator_state->ym_timer0 - 64 : 0;

			if (_emulator_state->ym_timer0 == 0)
				m_engine->engine_timer_expired(0);
		}

		if (_emulator_state->ym_timer1 != 0)
		{
			_emulator_state->ym_timer1 = _emulator_state->ym_timer1 - 64 > 0 ? _emulator_state->ym_timer1 - 64 : 0;

			if (_emulator_state->ym_timer1 == 0)
				m_engine->engine_timer_expired(1);
		}

		_ym_emulator.generate(&_ym_output);
		int left = _ym_output.data[0];
		if (left < -32768) left = -32768;
		if (left > 32767) left = 32767;
		_emulator_state->ym_left = left;

		int right = _ym_output.data[1];
		if (right < -32768) right = -32768;
		if (right > 32767) right = 32767;
		_emulator_state->ym_right = right;

		_emulator_state->memory[0x9F41] = _ym_emulator.read_status();
	}

	void write_register()
	{
		if (_emulator_state->ym_busy_timer != 0)
			return;
		_ym_emulator.write_address(_emulator_state->ym_address);
		_ym_emulator.write_data(_emulator_state->ym_data);

		_emulator_state->memory[0x9F41] = _ym_emulator.read_status();
	}
};

#endif