ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_output o0
dcl_output o1.xy
dcl_temps 1
lt r0.x, l(0.5), v0.x
ge r0.z, v0.z, v0.y
and o0.xz, r0.xz, l(0x3f800000, 0x00000000, 0x3f800000, 0x00000000)
mov o0.y, v0.w
mov o0.w, l(1)
eq r0.x, v0.z, l(0)
ne r0.y, v0.w, v0.x
and o1.xy, r0.xy, l(0x3f800000, 0x3f800000, 0x00000000, 0x00000000)
ret
