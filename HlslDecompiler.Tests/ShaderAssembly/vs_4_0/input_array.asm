vs_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input v0
dcl_input v1
dcl_input v2
dcl_input v3
dcl_output_siv o0, position
dcl_temps 1
dcl_indexrange v0 4
mov r0.x, cb0[0].x
mov o0, v[r0.x]
ret
