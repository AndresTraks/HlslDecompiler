cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps | enable11_1DoubleExtensions
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
dfma r0.xy, cb0[0].xy, cb0[0].zw, cb0[0].xy
dtof r0.x, r0.xy
store_structured u0.x, vThreadID.x, l(0), r0.x
drcp r0.xy, cb0[0].xy
dtof r0.x, r0.xy
iadd r0.yzw, vThreadID.xxx, l(0, 1, 2, 3)
store_structured u0.x, r0.y, l(0), r0.x
deq r0.x, cb0[0].zw, cb0[0].xy
dmovc r0.xy, r0.x, cb0[0].xy, cb0[0].zw
dtof r0.x, r0.xy
store_structured u0.x, r0.z, l(0), r0.x
dtoi r0.x, cb0[0].xy
itod r0.xy, r0.x
dtof r0.x, r0.xy
store_structured u0.x, r0.w, l(0), r0.x
ret
