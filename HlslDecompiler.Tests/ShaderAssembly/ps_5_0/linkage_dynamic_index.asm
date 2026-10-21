ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_body fb2
dcl_function_body fb3
dcl_function_table ft0 = {fb0, fb2}
dcl_function_table ft1 = {fb1, fb3}
dcl_interface fp0[2][2] = {ft0, ft1}
dcl_input_ps linear v0.xyz
dcl_input_ps linear v1.w
dcl_output o0
dcl_temps 3
dp3 r0.x, v0.xyz, v0.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v0.xyz
mov r0.w, cb0[0].x
fcall fp0[r0.w + 0][0]
iadd r0.x, -cb0[0].x, l(1)
fcall fp0[r0.x + 0][1]
add o0.xyz, r1.xyz, r2.xyz
mov o0.w, l(1)
ret
label fb0
mov r1.w, this[1048634].y
mov r2.x, this[1048634].x
mad r1.xyz, r0.zyx, cb[r2.x][r1.w].xxx, -v1.www
ret
label fb1
mov r1.w, this[1048634].y
mov r0.w, this[1048634].x
mad r1.xyz, r0.xyz, cb[r0.w][r1.w].xyz, v1.www
ret
label fb2
mov r0.yz, this[1048586].yx
mad r2.xyz, v0.yxz, cb[r0.z][r0.y].xxx, l(-1, -1, -1, 0)
ret
label fb3
mov r0.y, this[1048586].y
mov r0.x, this[1048586].x
mad r2.xyz, v0.zxy, cb[r0.x][r0.y].xyz, l(1, 1, 1, 0)
ret
