hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[3][0].xyz
dcl_output o0.xyz
dcl_output o0.w
dcl_temps 1
utof r0.x, vOutputControlPointID
mov r0.y, vOutputControlPointID
add o0.w, r0.x, v[r0.y][0].z
add o0.xyz, v[r0.y][0].xyz, v[r0.y][0].xyz
ret
hs_fork_phase
dcl_input vocp[3][0].y
dcl_input vocp[3][0].w
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o3.x, finalTriInsideTessFactor
dcl_temps 1
mov r0.xy, l(0, 0, 0, 0)
loop
uge r0.z, r0.y, cb0[0].y
breakc_nz r0.z
mad r0.x, vocp[r0.y][0].w, vocp[r0.y][0].y, r0.x
iadd r0.y, r0.y, l(1)
endloop
mul r0.x, r0.x, cb0[0].x
max r0.x, r0.x, l(1)
mov o0.x, r0.x
mov o3.x, r0.x
ret
hs_fork_phase
dcl_input vocp[3][0].x
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_temps 1
mul r0.x, cb0[0].x, vocp[1][0].x
max o1.x, r0.x, l(1)
ret
hs_fork_phase
dcl_input vocp[3][0].w
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
max o2.x, l(1), vocp[2][0].w
ret
