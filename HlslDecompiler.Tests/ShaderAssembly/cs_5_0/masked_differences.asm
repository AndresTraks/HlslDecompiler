cs_5_0
dcl_globalFlags refactoringAllowed | enable11_1ShaderExtensions
dcl_constantbuffer CB0[2], immediateIndexed
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
ushr r0.xyz, cb0[0].yyy, l(8, 16, 24, 0)
bfi r0.yzw, l(0, 8, 16, 24), l(0, 24, 16, 8), cb0[0].zzz, r0.xyz
mov r0.x, cb0[0].y
msad r0, cb0[0].x, r0, cb0[1]
store_structured u0, vThreadID.x, l(0), r0
ret
