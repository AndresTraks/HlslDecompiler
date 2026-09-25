cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 16
dcl_temps 2
dcl_thread_group 8, 1, 1
mov r0.x, l(0)
loop
ige r0.y, r0.x, cb0[0].x
breakc_nz r0.y
iadd r0.yz, r0.xx, l(0, 3, 1, 0)
and r0.y, r0.y, l(7)
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r1, r0.y, l(0), u0
add r1, r1, r1
store_structured u0, r0.x, l(0), r1
mov r0.x, r0.z
endloop
ret
