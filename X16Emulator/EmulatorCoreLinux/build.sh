gcc --version
gcc -shared -fno-pie -o EmulatorCore.so -fPIC core.cpp core.obj -Wall -m64 -z noexecstack
