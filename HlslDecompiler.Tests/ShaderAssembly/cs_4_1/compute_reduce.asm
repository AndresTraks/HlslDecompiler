cs_4_1
dcl_globalFlags refactoringAllowed
dcl_constantbuffer cb0[2], immediateIndexed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 64, 1, 1
mov r0, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
uge r1.y, r1.x, cb0[0].x
breakc_nz r1.y
ld_structured r2, r1.x, l(0), t0
mad r0, r2, cb0[1], r0
iadd r1.x, r1.x, l(1)
endloop
umax r1.x, cb0[0].x, l(1)
utof r1.x, r1.x
div r0, r0, r1.x
store_structured u0, vThreadID.x, l(0), r0
ret
