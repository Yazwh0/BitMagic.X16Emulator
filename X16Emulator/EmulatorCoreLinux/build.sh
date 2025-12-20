gcc --version
strip --strip-debug core.obj
gcc -shared -fno-pie -o EmulatorCore.so -fPIC core.cpp core.obj -Wall -m64 -g0 -z noexecstack
