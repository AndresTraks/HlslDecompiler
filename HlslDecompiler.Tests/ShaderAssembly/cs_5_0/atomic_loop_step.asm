cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 4
dcl_uav_structured u1, 4
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
mov r0.xy, l(0, 0, 0, 0)
loop
uge r0.z, r0.y, l(4)
breakc_nz r0.z
iadd r0.z, r0.y, l(1)
imm_atomic_cmp_exch r1.x, u0, l(0, 0, 0, 0), r0.y, r0.z
ieq r0.w, r0.y, r1.x
if_nz r0.w
mov r0.x, r1.x
break
endif
mov r0.y, r0.z
mov r0.x, r1.x
endloop
imul null, r0.x, r0.x, l(3)
store_structured u1.x, vThreadID.x, l(0), r0.x
ret
