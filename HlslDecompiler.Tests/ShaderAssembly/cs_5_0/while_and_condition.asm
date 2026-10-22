cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 8, 1, 1
imad r0.x, vThreadID.x, l(977), cb0[0].x
mov r1.x, r0.x
mov r0.y, l(0)
loop
ine r0.z, r1.x, l(0)
ult r0.w, r0.y, l(10)
and r0.z, r0.w, r0.z
breakc_z r0.z
udiv r1.x, r2.x, r1.x, cb0[0].y
iadd r0.y, r0.y, r2.x
endloop
store_structured u0.x, vThreadID.x, l(0), r0.y
ret
