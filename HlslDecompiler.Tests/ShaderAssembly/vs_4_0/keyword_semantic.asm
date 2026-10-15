vs_4_0
dcl_input v0.xyw
dcl_input v1
dcl_output_siv o0, position
dcl_output o1.x
dcl_temps 1
itof r0.x, v0.x
mad o0, v1, r0.x, v0.w
lt r0.x, l(0.5), v1.x
movc o1.x, r0.x, v1.y, v0.y
ret
