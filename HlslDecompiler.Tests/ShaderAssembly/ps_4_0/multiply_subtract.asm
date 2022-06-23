ps_4_0
dcl_input_ps linear v0.xyw
dcl_output o0.xyzw
mov o0.w, |v0.w|
mov o0.x, l(3.000000)
mad o0.yz, v0.xxyx, l(0.000000, 2.000000, 2.000000, 0.000000), l(0.000000, -1.000000, -1.000000, 0.000000)
ret 
