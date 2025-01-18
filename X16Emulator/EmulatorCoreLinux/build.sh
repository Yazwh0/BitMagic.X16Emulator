gcc -shared -fno-pie -o EmulatorCore.so -fPIC core.cpp core.obj -Wall -g -m64 -z noexecstack
