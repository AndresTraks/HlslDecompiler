cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
iadd r0.x, vThreadID.x, cb0[0].x
switch r0.x
case l(0)
mov r0.x, l(1065353216)
break
case l(2)
utof r0.y, vThreadID.x
mul r0.x, r0.y, l(0.5)
break
default
mov r0.x, l(7)
break
endswitch
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
