cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.y, l(0), l(0), u0.x
mov r0.z, r0.y
mov r0.w, l(0)
loop
ige r1.x, r0.w, l(16)
breakc_nz r1.x
ge r1.x, r0.z, r0.x
if_nz r1.x
break
endif
imm_atomic_cmp_exch r1.x, u0, l(0, 0, 0, 0), r0.z, r0.x
ieq r1.y, r0.z, r1.x
if_nz r1.y
break
endif
mov r0.z, r1.x
iadd r0.w, r0.w, l(1)
endloop
ret
