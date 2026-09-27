cs_5_0
dcl_globalFlags refactoringAllowed | enable11_1ShaderExtensions
dcl_constantbuffer CB0[2], immediateIndexed
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 64, 1, 1
iadd r0.x, cb0[0].x, l(1)
ushr r0.yzw, cb0[0].yyy, l(0, 8, 16, 24)
bfi r1.yzw, l(0, 8, 16, 24), l(0, 24, 16, 8), cb0[0].zzz, r0.yzw
mov r1.x, cb0[0].y
msad r2, cb0[0].x, r1, cb0[1]
msad r0, r0.x, r1, r2
store_structured u0, vThreadID.x, l(0), r0
ret
