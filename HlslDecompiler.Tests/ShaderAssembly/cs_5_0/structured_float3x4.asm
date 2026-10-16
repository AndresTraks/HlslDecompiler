cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 64
dcl_resource_structured t1, 16
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 8
dcl_thread_group 32, 1, 1
mov r0.w, l(1)
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r1, vThreadID.x, l(0), t1
ushr r2.x, vThreadID.x, l(2)
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r3.xyz, r2.xxx, l(0), t0.yxz
mov r4.x, r3.y
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r5.xyz, r2.xxx, l(12), t0.xzy
mov r4.y, r5.x
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r2.yzw, r2.xxx, l(24), t0.xyz
mov r4.z, r2.y
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r6, r2.x, l(36), t0
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r7.xyz, r2.xxx, l(52), t0.xyz
mov r4.w, r6.x
dp4 r0.x, r4, r1
mov r5.x, r3.z
mov r3.y, r5.z
mov r3.z, r2.z
mov r5.z, r2.w
mov r3.w, r6.y
dp4 r0.y, r3, r1
mov r5.w, r6.z
mov r7.w, r6.w
dp4 r0.z, r5, r1
mul r0, r0, r7.wxyz
store_structured u0, vThreadID.x, l(0), r0
ret
