cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps | enable11_1DoubleExtensions
dcl_constantbuffer CB0[2], immediateIndexed
dcl_uav_typed_buffer (float,float,float,float) u0
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
utod r0.xy, vThreadID.x
dmul r0.xy, r0.xy, cb0[0].xy
dadd r0.xy, r0.xy, cb0[0].zw
dtof r0.z, r0.xy
store_uav_typed u0, vThreadID.x, r0.z
dmax r0.zw, r0.xy, cb0[1].xy
dlt r0.x, r0.xy, cb0[1].xy
and r0.x, r0.x, l(0x3f800000)
dmin r0.zw, r0.zw, cb0[1].zw
dtof r0.y, r0.zw
iadd r1.xyz, vThreadID.xxx, l(1, 2, 3, 0)
store_uav_typed u0, r1.x, r0.y
store_uav_typed u0, r1.y, r0.x
ddiv r0.xy, cb0[0].xy, cb0[0].zw
dtof r0.x, r0.xy
store_uav_typed u0, r1.z, r0.x
ret
