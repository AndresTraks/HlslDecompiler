cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_texture2d (uint,uint,uint,uint) t0
dcl_uav_structured u0, 4
dcl_uav_typed_texture2d (uint,uint,uint,uint) u1
dcl_uav_structured u2, 4
dcl_input vThreadID.xy
dcl_temps 2
dcl_thread_group 8, 8, 1
mov r0.xy, vThreadID.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(uint,uint,uint,uint) r0.x, r0.x, t0.x
mul r0.y, r0.x, cb0[0].x
mad r0.x, r0.x, cb0[0].x, l(1)
store_uav_typed u1, vThreadID.xyyy, r0.x
store_structured u0.x, vThreadID.x, l(0), r0.y
add r0.x, r0.y, r0.y
imm_atomic_alloc r1.x, u2
store_structured u2.x, r1.x, l(0), r0.x
ret
