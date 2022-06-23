ps_4_0
dcl_input_ps linear v0.xyzw
dcl_output o0.xyzw
dcl_temps 1
ge r0.x, v0.x, l(0.000000)
mul r0.y, v0.y, l(3.000000)
add r0.z, v0.z, v0.z
movc o0.xyz, r0.xxxx, r0.yyyy, r0.zzzz
mov o0.w, v0.w
ret 
