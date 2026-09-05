ps_4_0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
add r0, -v0, v0.wzyx
mad r0, r0, l(0.25, 0.25, 0.25, 0.25), v0
Max
Min
add r0, r0, r1
Min
add r0, r0, r1
Max
add o0, r0, r1
ret
