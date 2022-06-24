ps_4_0
dcl_input_ps linear v0.yzw
dcl_output o0
dcl_temps 1
dp2 r0.x, v0.yzyy, v0.zwzz
add o0.x, r0.x, l(1)
mov o0.yzw, l(0, 2, 3, 4)
ret
