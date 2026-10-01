cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_uav_structured u1, 4
dcl_uav_structured u2, 4
dcl_input vThreadID.x
dcl_temps 4
dcl_thread_group 8, 1, 1
mov r0.y, l(0)
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
imm_atomic_and r1.x, u0, r0.xyxx, l(240)
imm_atomic_or r2.x, u0, r0.xyxx, l(15)
iadd r0.z, r1.x, r2.x
imm_atomic_xor r1.x, u0, r0.xyxx, l(170)
iadd r0.z, r0.z, r1.x
imm_atomic_imax r1.x, u0, r0.xyxx, l(7)
iadd r0.z, r0.z, r1.x
imm_atomic_imin r1.x, u0, r0.xyxx, l(3)
iadd r0.z, r0.z, r1.x
imm_atomic_exch r1.x, u0, r0.xyxx, l(9)
imm_atomic_umax r2.x, u1, r0.xyxx, l(7)
imm_atomic_umin r3.x, u1, r0.xyxx, l(3)
iadd r0.z, r0.z, r2.x
iadd r0.z, r3.x, r0.z
iadd r0.z, r1.x, r0.z
atomic_and u0, r0.xyxx, l(15)
atomic_or u0, r0.xyxx, l(240)
store_structured u2.x, r0.x, l(0), r0.z
ret
