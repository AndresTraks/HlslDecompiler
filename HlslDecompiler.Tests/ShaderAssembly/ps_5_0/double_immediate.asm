ps_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
ftod r0, v0.xy
dmul r0, r0, d(0.250000l, 3.000000l)
dadd r0, r0, d(1.500000l, 1.500000l)
dtof r0.xy, r0
ftod r0.zw, v0.x
dmul r0.zw, r0.zwzw, d(0.000000l, 2.000000l)
dadd r0.zw, r0.zwzw, d(0.000000l, 1.500000l)
dtof r1.x, r0.zw
dlt r0.z, d(4.000000l, 0.000000l), r0.zw
and o0.z, r0.z, l(0x3f800000)
add o0.xy, r0.xy, r1.xx
mov o0.w, l(0)
ret
