/* Reference values from the real x87 instructions, for checking the port's
 * deterministic trig (OpenPax.Sim.Numerics) against what the original
 * computed. Each case mirrors an instruction sequence of the original under
 * its FPU control word: 53-bit precision, round to nearest.
 *
 *   tools/x87_reference OUT [COUNT]
 *
 * Writes records of { uint8 op, double a, double b, double result }:
 *   op 0: round53(fcos(a) * (int)b)   fild/fimul form, orbit positions
 *   op 1: round53(fsin(a) * (int)b)
 *   op 2: round53(fcos(a) * b)        fmul by a double, wormholes
 *   op 3: round53(fsin(a) * b)
 *   op 4: angle in degrees from fpatan(dy = a, dx = b), as
 * Inputs follow how the game forms them: radians from tenth-degree angles,
 * orbit radii, and integer star offsets. Only runs on x86. */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>

static const double kPi = 3.141593;            /* the game's constant */
static const double kInv180 = 0.005555555555555556;
static const double kInvPi = 0.3183098510851024; /* 1 / 3.141593 */

static void set_control_word(void)
{
    uint16_t cw = 0x027F; /* all exceptions masked, 53-bit precision, round to nearest */
    __asm__ volatile("fldcw %0" : : "m"(cw));
}

static double trig_int(double a, int32_t r, int sine)
{
    double out;
    if (sine)
        __asm__ volatile("fldl %1\n\tfsin\n\tfimull %2\n\tfstpl %0" : "=m"(out) : "m"(a), "m"(r));
    else
        __asm__ volatile("fldl %1\n\tfcos\n\tfimull %2\n\tfstpl %0" : "=m"(out) : "m"(a), "m"(r));
    return out;
}

static double trig_double(double a, double m, int sine)
{
    double out;
    if (sine)
        __asm__ volatile("fldl %1\n\tfsin\n\tfmull %2\n\tfstpl %0" : "=m"(out) : "m"(a), "m"(m));
    else
        __asm__ volatile("fldl %1\n\tfcos\n\tfmull %2\n\tfstpl %0" : "=m"(out) : "m"(a), "m"(m));
    return out;
}

/* Mirrors for dx != 0 and dy != 0. */
static double angle_deg(int32_t dy, int32_t dx)
{
    double out;
    double c180 = 180.0, inv = kInvPi, zero = 0.0, m360 = -360.0;
    __asm__ volatile(
        "fildl %1\n\t"   /* st0 = dy */
        "fildl %2\n\t"   /* st0 = dx, st1 = dy */
        "fpatan\n\t"     /* st0 = atan2(dy, dx) */
        "fmull %3\n\t"
        "fmull %4\n\t"
        "fstpl %0"
        : "=m"(out) : "m"(dy), "m"(dx), "m"(c180), "m"(inv));
    if (out < zero)
    {
        double t;
        __asm__ volatile("fldl %1\n\tfsubl %2\n\tfstpl %0" : "=m"(t) : "m"(out), "m"(m360));
        out = t;
    }
    return out;
}

static uint64_t state = 88172645463325252ull;
static uint64_t next(void)
{
    state ^= state << 13;
    state ^= state >> 7;
    state ^= state << 17;
    return state;
}

static void put(FILE *f, uint8_t op, double a, double b, double r)
{
    fwrite(&op, 1, 1, f);
    fwrite(&a, 8, 1, f);
    fwrite(&b, 8, 1, f);
    fwrite(&r, 8, 1, f);
}

int main(int argc, char **argv)
{
    if (argc < 2)
    {
        fprintf(stderr, "usage: x87_reference OUT [COUNT]\n");
        return 2;
    }

    long count = argc > 2 ? atol(argv[2]) : 200000;
    FILE *f = fopen(argv[1], "wb");
    if (!f)
        return 1;
    set_control_word();

    static const int radii[] = {38, 64, 90, 116, 152, 178, 20, 21, 22, 23, 24, 30};
    const int nradii = sizeof radii / sizeof radii[0];

    /* Every starting angle the generator can draw, once per radius. */
    for (int k = 0; k < 3600; k++)
    {
        double rad = (k * 0.1) * kPi * kInv180;
        for (int i = 0; i < nradii; i++)
        {
            put(f, 0, rad, radii[i], trig_int(rad, radii[i], 0));
            put(f, 1, rad, radii[i], trig_int(rad, radii[i], 1));
        }
    }

    /* Angles after any number of orbit steps: any double in [0, 360). */
    for (long n = 0; n < count; n++)
    {
        double deg = (double)(next() >> 11) * (360.0 / 9007199254740992.0);
        double rad = deg * kPi * kInv180;
        int r = radii[next() % nradii];
        put(f, 0, rad, r, trig_int(rad, r, 0));
        put(f, 1, rad, r, trig_int(rad, r, 1));
        put(f, 2, rad, 175.0, trig_double(rad, 175.0, 0));
        put(f, 3, rad, 175.0, trig_double(rad, 175.0, 1));
    }

    /* Wormhole directions between star positions (offsets up to +-410). */
    for (long n = 0; n < count; n++)
    {
        int32_t dy = (int32_t)(next() % 821) - 410;
        int32_t dx = (int32_t)(next() % 821) - 410;
        if (dx == 0 || dy == 0)
            continue;
        put(f, 4, dy, dx, angle_deg(dy, dx));
    }

    fclose(f);
    return 0;
}
