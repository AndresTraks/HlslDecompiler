vs_4_0
dcl_constantbuffer cb0[10], immediateIndexed
dcl_output_siv o0, position
dcl_temps 1
mov r0.x, cb0[9].x
add o0, cb0[0], cb0[0]
ret
