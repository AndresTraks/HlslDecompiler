ps_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps | enable11_1DoubleExtensions
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 2
ftod r0, v0.xy
dmul r0.zw, r0.zwzw, cb0[0].xyxy
dne r1.x, r0.xy, r0.zw
if_nz r1.x
mov o0, l(1, 0, 0, 1)
ret
endif
dge r1.x, r0.xy, r0.zw
if_nz r1.x
mov o0, l(0, 1, 0, 1)
ret
endif
dlt r1.x, r0.xy, r0.zw
dadd r0.xy, r0.xy, d(1.000000l, 0.000000l)
dmul r1.zw, r0.zwzw, d(0.000000l, 2.000000l)
dmovc r0.xy, r1.x, r0.xy, r1.zw
dtou r0.z, r0.zw
utod r0.zw, r0.z
dadd r0.xy, r0.zw, r0.xy
dtof r0.x, r0.xy
mov o0.x, r0.x
mov o0.yzw, l(0, 0, 0, 1)
ret
