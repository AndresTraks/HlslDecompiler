vs_4_0
dcl_input v0.xyz
dcl_input v1
dcl_input v2
dcl_input v3.xy
dcl_output_siv o0, position
dcl_output o1
dcl_temps 1
itof r0.xyz, v0.xyz
mov r0.w, l(1)
mad o0, r0, v1, v2
mov r0.xz, v3.xy
mov r0.yw, l(0, 1, 0, 1)
mad o1, r0, v1, v2
ret
