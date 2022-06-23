ps_4_0
dcl_input_ps linear v0.xyz
dcl_output o0.xyzw
dcl_temps 1
mul r0.x, v0.x, v0.y
mul r0.x, r0.x, v0.z
mov o0.x, -|r0.x|
mov o0.y, r0.x
mov o0.zw, l(0,0,1.000000,2.000000)
ret 
