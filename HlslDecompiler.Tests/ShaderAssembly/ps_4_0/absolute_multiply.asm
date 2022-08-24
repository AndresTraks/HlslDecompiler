ps_4_0
dcl_input_sv linear v0.xw
dcl_output o0
dcl_temps 1
mul r0.x, v0.x, l(3)
mov o0.w, |r0.x|
mad o0.xy, v0.xw, l(3, 3, 0, 0), l(-1, -1, 0, 0)
mov o0.z, l(8)
ret
