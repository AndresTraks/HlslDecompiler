cs_4_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 32
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 64, 1, 1
ld_structured r0, vThreadID.x, l(0), u0
ld_structured r1, vThreadID.x, l(16), u0
mov r2.xz, l(0, 0, 0, 0)
mul r2.y, cb0[0].x, -cb0[0].y
add r2.xyz, r1.xyz, r2.xyz
mul r2.w, r1.w, cb0[0].z
mad r1.xyz, r2.xyz, cb0[0].xxx, r0.xyz
add r1.w, r0.w, -cb0[0].x
store_structured u0, vThreadID.x, l(0), r1
store_structured u0, vThreadID.x, l(16), r2
ret
