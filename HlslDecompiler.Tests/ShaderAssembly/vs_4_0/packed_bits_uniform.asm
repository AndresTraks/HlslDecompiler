vs_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input v0.xyz
dcl_input v1.x
dcl_output_siv o0, position
dcl_temps 1
mul r0.xyz, v0.xyz, v1.xxx
mad o0.xyz, r0.xyz, cb0[0].xxx, cb0[0].yyy
mov o0.w, l(1)
ret
