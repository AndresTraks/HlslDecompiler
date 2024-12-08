ps_4_0
dcl_input_ps linear v0.xyw
dcl_output o0
mov o0.w, |v0.w|
mov o0.x, l(3)
mad o0.yz, v0.xy, l(0, 2, 2, 0), l(0, -1, -1, 0)
ret
