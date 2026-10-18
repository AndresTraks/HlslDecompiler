cs_5_0
dcl_globalFlags refactoringAllowed | enable11_1ShaderExtensions
dcl_constantbuffer CB0[2], immediateIndexed
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ushr r0.xy, cb0[0].yy, l(8, 16, 0, 0)
bfi r0.yz, l(0, 8, 16, 0), l(0, 24, 16, 0), cb0[0].zz, r0.xy
mov r0.xw, cb0[0].yy
msad r0, cb0[0].x, r0, cb0[1].xyzx
store_structured u0, vThreadID.x, l(0), r0.wwyz
iadd r1.x, vThreadID.x, l(64)
store_structured u0, r1.x, l(0), r0
ret
