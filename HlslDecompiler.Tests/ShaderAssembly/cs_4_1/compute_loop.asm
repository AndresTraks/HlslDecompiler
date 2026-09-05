cs_4_1
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 64, 1, 1
ishl r0.x, vThreadID.x, l(2)
mov r1, l(0, 0, 0, 0)
mov r0.y, l(0)
loop
uge r0.z, r0.y, l(4)
breakc_nz r0.z
iadd r0.z, r0.y, r0.x
ld_structured r2, r0.z, l(0), t0
add r1, r1, r2
iadd r0.y, r0.y, l(1)
endloop
store_structured u0, vThreadID.x, l(0), r1
ret
