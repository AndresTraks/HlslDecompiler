ps_4_0
dcl_input_ps_siv linear noperspective v0.x, position
dcl_output o0
dcl_temps 1
round_z r0.x, v0.x
add o0.xzw, -r0.xxx, v0.xxx
mov o0.y, r0.x
ret
