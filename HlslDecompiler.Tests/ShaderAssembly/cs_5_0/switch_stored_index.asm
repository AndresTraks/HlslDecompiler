cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroupFlattened
dcl_temps 2
dcl_thread_group 64, 1, 1
switch cb0[0].x
case l(0)
mov r0.x, vThreadIDInGroupFlattened.x
mov r0.y, l(0)
loop
uge r0.z, r0.y, cb0[0].y
breakc_nz r0.z
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.z, r0.y, l(0), t0.x
lt r0.z, r0.z, l(0)
if_nz r0.z
iadd r1.y, r0.y, l(1)
mov r1.x, r0.x
mov r0.xy, r1.xy
continue
endif
iadd r0.x, r0.y, r0.x
store_structured u0.x, r0.x, l(0), r0.x
iadd r0.y, r0.y, l(1)
endloop
break
case l(1)
mov r0.x, vThreadIDInGroupFlattened.x
mov r0.y, l(0)
loop
uge r0.z, r0.y, cb0[0].y
breakc_nz r0.z
ishl r0.z, r0.x, l(1)
iadd r0.x, r0.y, r0.z
store_structured u0.x, r0.y, l(0), r0.x
iadd r0.y, r0.y, l(1)
endloop
break
default
mov r0.x, l(0)
break
endswitch
store_structured u0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
ret
