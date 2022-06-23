ps_4_0
dcl_input_ps linear v0.xw
dcl_output o0.xyzw
dcl_temps 1
mul r0.x, v0.x, l(3.000000)
mov o0.w, |r0.x|
mad o0.xy, v0.xwxx, l(3.000000, 3.000000, 0.000000, 0.000000), l(-1.000000, -1.000000, 0.000000, 0.000000)
mov o0.z, l(8.000000)
ret 
