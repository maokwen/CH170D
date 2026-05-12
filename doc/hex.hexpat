#pragma endian big
struct Pattern {
    u8 begin;
    u8 prefix[5];
    u8 mode;
    u16 cpu_power;
    padding[1];
    float cpu_temp;
    u8 cpu_locad;
    u16 cpu_freq;
    u16 fan_rpm;
    u16 gpu_power;
    float gpu_temp;
    u8 gpu_load;
    u16 gpu_freq;
    u16 psu_used;
    float psu_temp;
    u8 psu_load;
    u16 psu_total;
    u16 psu_fan;
    padding[1];
    u8 checksum;
    u8 end;
};

Pattern p1 @ 0x00;
Pattern p2 @ 0x40;
Pattern p3 @ 0x80;
Pattern p4 @ 0xC0;
