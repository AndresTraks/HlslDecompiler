ps_4_0
dcl_input_ps linear v0.xz
dcl_output o0
mov o0.x, -|v0.z|
mov o0.y, v0.x
mov o0.zw, l(0, 0, 1, 2)
ret
