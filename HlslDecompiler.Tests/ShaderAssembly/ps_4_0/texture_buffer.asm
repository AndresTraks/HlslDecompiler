ps_4_0
dcl_resource_buffer (mixed,mixed,mixed,mixed) t0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
ld r0, l(0, 0, 0, 0), t0
ld r1, l(1, 1, 1, 1), t0
mad o0, v0, r0, r1
ret
