cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
mov r0.xy, l(0, 0, 0, 0)
loop
ige r0.z, r0.y, l(8)
breakc_nz r0.z
switch cb0[0].x
case l(0)
itof r0.w, r0.y
add r0.z, r0.w, r0.x
break
case l(1)
add r0.z, r0.x, l(100)
break
case l(2)
ilt r0.w, l(4), r0.y
if_nz r0.w
mov r0.z, r0.x
break
endif
add r0.z, r0.x, l(200)
break
default
mov r0.z, r0.x
break
endswitch
and r0.w, r0.y, l(1)
if_nz r0.w
iadd r0.w, r0.y, l(1)
mov r0.x, r0.z
mov r0.y, r0.w
continue
endif
iadd r0.y, r0.y, l(1)
mov r0.xy, r0.zy
endloop
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
