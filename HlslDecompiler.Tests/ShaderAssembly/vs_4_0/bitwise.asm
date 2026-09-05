vs_4_0
dcl_constantbuffer cb0[1], immediateIndexed
DclInputSgv
dcl_output_siv o0, position
dcl_temps 1
and r0.x, v0.x, l(7)
xor r0.y, cb0[0].x, l(3)
or r0.x, r0.y, r0.x
utof o0, r0.x
ret
