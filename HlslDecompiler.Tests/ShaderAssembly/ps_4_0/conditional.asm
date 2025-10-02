ps_4_0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 1
ge r0.x, v0.x, l(0)
mul r0.y, v0.y, l(3)
add r0.z, v0.z, v0.z
movc o0.xyz, r0.xxx, r0.yyy, r0.zzz
mov o0.w, v0.w
ret
