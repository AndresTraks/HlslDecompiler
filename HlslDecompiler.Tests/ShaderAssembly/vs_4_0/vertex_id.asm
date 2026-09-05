vs_4_0
dcl_constantbuffer cb0[2], immediateIndexed
DclInputSgv
DclInputSgv
dcl_output_siv o0, position
dcl_temps 2
utof r0.x, v1.x
mul r0, r0.x, cb0[1]
utof r1.x, v0.x
mad o0, cb0[0], r1.x, r0
ret
